# Reading sources from databases

The Modernization Score can read its eight sources straight from the organization's databases instead of uploaded files. This guide explains how it works, where to configure it, the supported ways to connect, and how to troubleshoot. The design decision is recorded in [ADR 0037](adr/0037-fuentes-leidas-desde-bases-de-datos.md).

## Status

| Capability | State |
|---|---|
| Read sources from SQL Server, PostgreSQL, Oracle or any ODBC driver | Available, off by default |
| Test the connections from the *Data* screen | Available |
| Publish a version from the databases on demand (*Pull from databases*) | Available |
| Publish a version on a schedule (`PullInterval`) | Available |
| Mix database sources and uploaded files in one version | Not available: a pull is a complete upload |
| Store the application's own versions in a production database | Still pending (RFC-07); today the store is the `File` adapter or the SQLite-backed `Database` adapter |

## How it works

1. A person with the upload role and scope over the whole portfolio presses **Pull from databases and recalculate** on the *Data* screen. Alternatively, the schedule fires.
2. The System layer opens each configured connection with verified TLS. It starts a transaction, makes it read-only where the engine supports that, and runs the source's view or query.
3. The rows are written as a CSV with invariant values (dates `yyyy-MM-dd`, a dot as the decimal separator) to a private temporary directory.
4. Those CSVs go through **the same publisher as an uploaded file**:
   - the source is identified;
   - columns are recognized by name, alias or the remembered mapping;
   - every row is validated;
   - the version is published atomically.

   The upload limits apply: 500,000 rows and 128 MB per source, 256 MB in total.
5. The transaction is always rolled back and the temporary files are deleted.

If any source cannot be read, **nothing is published** and the current portfolio stays as it was. A version without that source would look as if the source had stopped reporting.

## Where to configure

Everything is deployment configuration. No screen can define a connection or a query, and no API response ever contains a connection string, a server name or a query.

| What | Where | Notes |
|---|---|---|
| Turn the feature on, the connections and the sources (no secrets) | The `Connectors` section of [`src/system/Modernization.SystemApi/appsettings.json`](../src/system/Modernization.SystemApi/appsettings.json), or `Connectors__…` environment variables in [`deploy/k8s/system-api.yaml`](../deploy/k8s/system-api.yaml) | Only the System layer reads databases. The Channel layer needs no change. |
| Passwords, client secrets, full connection strings | Files mounted from the secrets manager | See [Secrets](#secrets). Never in `appsettings*.json`, never in plain environment variables. |
| Network access to each database server | A NetworkPolicy in [`deploy/k8s/network-policies.yaml`](../deploy/k8s/network-policies.yaml) | Everything is denied by default. Open exactly each server's address and port. |
| Certificates, Oracle wallets, Kerberos keytabs | Extra CSI volumes mounted read-only into the System pod | Reference their paths from the connection string. |
| Where versions are stored | `DataSource:Provider` must be `File` or `Database` | With `Dummy` the process refuses to start when connectors are on. |

The process validates the `Connectors` section at startup. A configuration that cannot work stops the start and lists every problem at once: an unknown source, a missing connection, an invalid engine name, a query that is not a single `SELECT`, or an interval that is too short. Secrets and TLS are checked when connecting, so use **Test connections** after every change.

### Configuration reference

```jsonc
"Connectors": {
  "Enabled": true,                 // false by default
  "ConnectTimeoutSeconds": 30,     // 1–300
  "CommandTimeoutSeconds": 300,    // 1–3600, per query
  "PullInterval": null,            // e.g. "06:00:00"; at least 00:15:00; null = on demand only
  "Connections": {
    "<connection name>": {
      "Provider": "SqlServer | PostgreSql | Oracle | Odbc",
      "ConnectionString": "…",          // without the password, or comes from the secrets directory
      "ConnectionStringFile": null,     // a file holding the whole connection string
      "PasswordFile": null,             // a file holding only the password; appended when connecting
      "EncryptionEnforcedByDriver": false, // ODBC only, see below
      "AllowInsecureInDevelopment": false  // ignored outside Development
    }
  },
  "Sources": {
    "<source key>": {
      "Connection": "<connection name>",
      "Table": "SCHEMA.VIEW_NAME",  // either Table…
      "Query": null                 // …or a single read-only SELECT / WITH … SELECT
    }
  }
}
```

### Source keys

A source is keyed by its canonical file name without `.csv`. A source that is not listed is not read, and `eai_master` must always be listed: without the application inventory there is nothing to score.

| Key | Source |
|---|---|
| `eai_master` | EAI Master: the application inventory, one row per EAI |
| `architectural_value_by_eai` | Architectural Value |
| `product_strategy` | Product Strategy |
| `tech_debt` | Tech Debt (only `COMBINED_APP_SCORE` is loaded) |
| `veracode_sca_open_flaws` | SCA Flaws |
| `oss_score_by_eai` | OSS Score |
| `monitoreo` | Monitoring |
| `apim_onboarded_eais` | APIM Onboarding |

The view or query must return the columns of the source's contract in [`contracts/sources/source-schemas.json`](../contracts/sources/source-schemas.json). It may use the contract name or any accepted alias, the same names the *Mapping* tab lists. Rename with `AS` when the database uses other names. Every column needs a name, and binary columns are refused. The recommended form is a dedicated view per source, with `Table`.

### Secrets

The System layer already loads one-key-per-file secrets from `Secrets:Directory`. In the Kubernetes deployment that is `/mnt/secrets`, mounted by the Secrets Store CSI driver. A double underscore in a file name stands for a configuration section separator, so the file

```
/mnt/secrets/Connectors__Connections__cmt__ConnectionString
```

supplies `Connectors:Connections:cmt:ConnectionString`. Map it in the `SecretProviderClass` named `system-api-secrets`, for example with Azure Key Vault:

```yaml
objects: |
  array:
    - |
      objectName: modscore-cmt-connection-string
      objectType: secret
      objectAlias: Connectors__Connections__cmt__ConnectionString
```

To keep the non-secret part visible in configuration, set `ConnectionString` without the password and mount only the password. Then point `PasswordFile` at it, for example `/mnt/secrets/cmt-password`. `ConnectionStringFile` does the same for a whole connection string mounted somewhere else.

A secret file must exist, must not be empty, and must be at most 16 KB. Error messages never include its path or content.

### Example: Kubernetes

```yaml
# deploy/k8s/system-api.yaml, container env
- name: Connectors__Enabled
  value: "true"
- name: Connectors__Connections__cmt__Provider
  value: SqlServer
- name: Connectors__Connections__cmt__ConnectionString
  value: "Server=tcp:cmt.database.windows.net,1433;Database=cmt;Authentication=Active Directory Workload Identity;Encrypt=Strict"
- name: Connectors__Sources__eai_master__Connection
  value: cmt
- name: Connectors__Sources__eai_master__Table
  value: MODSCORE.V_EAI_MASTER
```

```yaml
# deploy/k8s/network-policies.yaml: open exactly each database server
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata: { name: system-api-sources-egress, namespace: modernization-score }
spec:
  podSelector:
    matchLabels:
      app.kubernetes.io/name: system-api
  policyTypes: ["Egress"]
  egress:
    - to:
        - ipBlock: { cidr: 10.20.30.40/32 }   # the server, not its whole subnet
      ports:
        - { protocol: TCP, port: 1433 }
```

The pod's `/tmp` volume (`sizeLimit: 600Mi`) holds the temporary CSVs of a pull, up to 256 MB, and the buffer of file uploads. Keep that limit.

## Connection methods

Every connection must use **verified TLS**. The rules per engine are below. Outside Development a connection that breaks them is never opened, and the *Data* screen says which connection and why.

### SQL Server and Azure SQL

TLS rule: `Encrypt=Mandatory` (the driver default) or `Encrypt=Strict`, and never `TrustServerCertificate=True`. The application connects with `ApplicationIntent=ReadOnly`, so in an availability group it reaches a readable secondary.

| Method | Connection string | Secret |
|---|---|---|
| SQL login | `Server=sql01.corp.example;Database=cmt;User ID=modscore_reader;Encrypt=Mandatory` | Password in `PasswordFile` |
| Windows integrated / Kerberos | `Server=sql01.corp.example;Database=cmt;Integrated Security=SSPI;Encrypt=Mandatory` | A keytab and `krb5.conf` mounted in the pod, with a ticket kept by a `kinit` sidecar. The Linux image needs the Kerberos libraries (`libkrb5`, `libgssapi-krb5`). |
| Microsoft Entra managed identity | `Server=tcp:cmt.database.windows.net,1433;Database=cmt;Authentication=Active Directory Managed Identity;Encrypt=Strict` (add `User ID=<client id>` for a user-assigned identity) | None |
| Microsoft Entra workload identity (AKS) | `…;Authentication=Active Directory Workload Identity;Encrypt=Strict` | None: the federated token comes from the service account |
| Microsoft Entra service principal | `…;Authentication=Active Directory Service Principal;User ID=<application id>;Encrypt=Strict` | Client secret in `PasswordFile` |
| Microsoft Entra default chain | `…;Authentication=Active Directory Default;Encrypt=Strict` | Whatever the chain finds |

Sign-ins that need a person (`Active Directory Interactive`, `Active Directory Device Code Flow`) are refused. Use `HostNameInCertificate=` when the certificate's name differs from the server name you connect to.

### PostgreSQL

TLS rule: `SSL Mode=VerifyFull` (recommended) or `VerifyCA`. Use `Root Certificate=` when the server's CA is not in the image's trust store.

| Method | Connection string | Secret |
|---|---|---|
| Password | `Host=pg.corp.example;Database=sca;Username=modscore_reader;SSL Mode=VerifyFull` | Password in `PasswordFile` |
| Client certificate | `Host=pg.corp.example;Database=sca;Username=modscore_reader;SSL Mode=VerifyFull;Root Certificate=/mnt/pg/ca.crt;SSL Certificate=/mnt/pg/client.crt;SSL Key=/mnt/pg/client.key` | Certificate and key mounted read-only. If the key has a passphrase, put the whole connection string, with `SSL Password`, in a secret file (`ConnectionStringFile` or the secrets directory) |
| Kerberos (GSSAPI) | `Host=pg.corp.example;Database=sca;Username=modscore_reader@CORP.EXAMPLE;Kerberos Service Name=postgres;SSL Mode=VerifyFull` | Keytab and ticket as for SQL Server; no password |

### Oracle

TLS rule: the TCPS protocol, either in the descriptor (`(PROTOCOL=TCPS)`) or as `tcps://host:port/service`. A TNS alias cannot be checked, so paste the alias's descriptor from `tnsnames.ora` into `Data Source`.

| Method | Connection string | Secret |
|---|---|---|
| Password over TCPS | `User Id=modscore_reader;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST=ora.corp.example)(PORT=2484))(CONNECT_DATA=(SERVICE_NAME=CMT))(SECURITY=(SSL_SERVER_DN_MATCH=yes)))` | Password in `PasswordFile` |
| Wallet (mutual TLS, Autonomous Database) | `User Id=/;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST=adb.example.oraclecloud.com)(PORT=1522))(CONNECT_DATA=(SERVICE_NAME=cmt_high.adb.oraclecloud.com)));Wallet_Location=/mnt/oracle-wallet` | The wallet directory mounted read-only. With `User Id=/` the credentials come from the wallet; otherwise use a user plus `PasswordFile`. |

The application makes the transaction read-only with `SET TRANSACTION READ ONLY`, which PostgreSQL also uses.

### ODBC

For any other engine with an ODBC driver: `DSN=TechDebt` or `Driver={…};Server=…;Database=…;UID=modscore_reader`. A password in `PasswordFile` is appended as `PWD`.

An ODBC connection string does not reveal whether the driver encrypts and validates the server certificate. Configure that in the DSN or the driver, confirm it, and then set `"EncryptionEnforcedByDriver": true`. Without it, the connection is refused.

The container image does not include an ODBC driver manager or drivers. Install `unixODBC` and the vendor's driver in a derived image, and keep its packages under the same vulnerability scanning as the rest.

## Security requirements

- **A read-only account per connection.** Grant `SELECT` on the source views only, nothing else. The application adds three more controls:
  - every statement runs in a transaction that is always rolled back;
  - the transaction is read-only where the engine allows it;
  - the configured query is checked for shape: a single `SELECT` or `WITH … SELECT`, no `;`, no comments, no `INTO`, no `FOR UPDATE`, and a `Table` that is a plain name.

  These are defenses in depth, not a substitute for the account's permissions.
- **Verified TLS on every connection**, enforced by the application (see above).
- **Secrets only in mounted files.** Rotating one needs no redeploy: the file is read on every connection.
- **Network least privilege.** One egress rule per server, by address and port.
- **Who can pull.**
  - Pressing *Pull* publishes a version, so it requires the upload role, scope over the whole portfolio and the CSRF token, under the upload rate limit.
  - The System endpoints (`/system/ingestion/connectors`, `/system/ingestion/connectors/test`, `/system/ingestion/pull`) also require full scope.
- **Audit.**
  - Every pull and every connection test is a security event with a stable id: `4215` published, `4216` rejected, `4217` connections tested.
  - Operational logs use `5501`–`5504` (source read, connection refused by a rule, connection or query failed, cleanup failed) and `5511`–`5513` for the schedule.
  - The database's own error message stays in the System log and never reaches the browser.

## Scheduling

`PullInterval` (at least 15 minutes) makes the System layer read and publish on its own, as the author `connector-schedule`. Every pull publishes a version, so with several System replicas set it on one of them only, for example a dedicated single-replica deployment. A failed scheduled pull is logged and retried at the next interval. The current portfolio stays as it was.

## Using it

On the *Data* screen, people who can publish see **Database sources**:

- which connection and engine each source comes from;
- whether it is read from a view or a query.

**Test connections** opens each connection and runs `SELECT 1` (`SELECT 1 FROM DUAL` on Oracle) without reading data. **Pull from databases and recalculate** publishes the upload and shows:

- the per-source detail: rows read, time and result;
- the same per-file verdict as an upload: rejected rows, values loaded as empty.

## Local development

In the Development environment a connection may set `"AllowInsecureInDevelopment": true` to reach, for example, a local container without TLS. The flag is ignored in every other environment.

```bash
docker run --rm -e POSTGRES_PASSWORD=local -p 5432:5432 postgres:17
```

```jsonc
// src/system/Modernization.SystemApi/appsettings.Development.json (not committed with real values)
"Connectors": {
  "Enabled": true,
  "Connections": {
    "local": { "Provider": "PostgreSql", "ConnectionString": "Host=localhost;Database=postgres;Username=postgres;Password=local", "AllowInsecureInDevelopment": true }
  },
  "Sources": { "eai_master": { "Connection": "local", "Table": "public.eai_master" } }
}
```

## Troubleshooting

Each problem appears on the *Data* screen with its code. The System log has the detail, under event `5502` or `5503`.

| Code | Meaning | What to do |
|---|---|---|
| `connector.tlsRequired` | The connection would not use verified TLS | Apply the TLS rule of its engine above |
| `connector.secretMissing` | No connection string, or a secret file is missing, empty, unreadable or over 16 KB | Check the CSI mount and the `objectAlias` |
| `connector.connectionStringInvalid` | The driver rejected the connection string | Check the keywords of that engine |
| `connector.providerUnknown` | `Provider` is not `SqlServer`, `PostgreSql`, `Oracle` or `Odbc` | Fix the name |
| `connector.authenticationInteractive` | A SQL Server sign-in that needs a person | Use a login, Kerberos, managed or workload identity, or a service principal |
| `connector.connectFailed` | The server could not be reached or refused the sign-in | Check the NetworkPolicy, DNS, the account and the certificate. The log has the database's message |
| `connector.queryFailed` | The view or query failed | Check the object name and the account's `SELECT` grant |
| `connector.queryInvalid` | The query is not a single read-only `SELECT`, or `Table` is not a plain name | Use a view and `Table` |
| `connector.timeout` | Connecting or reading took longer than allowed | Raise `ConnectTimeoutSeconds` or `CommandTimeoutSeconds`, or filter the view |
| `connector.tooLarge` | The source exceeds the upload limits | Filter or aggregate in the view |
| `connector.columnUnnamed` | A column has no name | Add `AS` |
| `connector.columnUnsupported` | A column has a type a source cannot hold, such as binary | Convert it in the query |
| `connector.pullFailed` | One or more sources could not be read, so nothing was published | See the per-source detail |
| `connector.busy` | Another pull is running | Wait for it to finish |
| `connector.disabled` | Connectors are off, or the data adapter does not accept uploads | Set `Connectors:Enabled` and use the `File` or `Database` adapter |

Problems with the rows themselves (`row.*`, `csv.missingColumns` and so on) are the same as for an uploaded file. The *Mapping* tab shows which column name each field accepts.

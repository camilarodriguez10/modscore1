import type { Notice } from '../api/types';
import { areaName, constraintText, parameterLabel, statusLabel } from './analytics';

/**
 * Catalogo de frases del sitio.
 *
 * El backend no redacta texto para personas: devuelve `{ code, args }` y este
 * catalogo arma la frase (ADR 0017). Asi el idioma vive en un solo lugar, las
 * pruebas del backend comparan codigos, y un segundo idioma es otro catalogo con
 * las mismas claves.
 *
 * Una prueba del backend recorre `NoticeCodes` y falla si algun codigo no esta
 * aqui: un codigo nuevo sin frase no puede llegar a produccion.
 */

type Args = Readonly<Record<string, string>>;

const numberFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const scoreFormat = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Un argumento numerico con formato en ingles; si no es numero, tal cual. */
function num(value: string | undefined): string {
  const parsed = Number(value);
  return value === undefined || value === '' || Number.isNaN(parsed) ? (value ?? '') : numberFormat.format(parsed);
}

/** Un score, siempre con dos decimales como en el resto del sitio. */
function score(value: string | undefined): string {
  const parsed = Number(value);
  return value === undefined || value === '' || Number.isNaN(parsed) ? (value ?? '') : scoreFormat.format(parsed);
}

function signed(value: string | undefined): string {
  const parsed = Number(value);
  if (Number.isNaN(parsed)) return value ?? '';
  return `${parsed >= 0 ? '+' : ''}${scoreFormat.format(parsed)}`;
}

function plural(count: string | undefined, singular: string, many = `${singular}s`): string {
  return count === '1' ? singular : many;
}

const COMPOSITE_CATEGORIES: Record<string, string> = {
  modern: 'modern',
  heritage: 'heritage',
  obsolete: 'obsolete',
  notApprovedForUse: 'not approved for use',
  invest: 'invest',
  tolerate: 'tolerate',
  eliminate: 'eliminate',
  migrate: 'migrate',
};

/** "15xmodern + 1xheritage" -> "15 modern + 1 heritage". */
function counts(value: string | undefined): string {
  return (value ?? '')
    .split(' + ')
    .filter(Boolean)
    .map((part) => {
      const [count, key = ''] = part.split('x');
      return `${count} ${COMPOSITE_CATEGORIES[key] ?? key}`;
    })
    .join(' + ');
}

/** Las partes de un borrador de reglas, como las nombra la pantalla. */
const DRAFT_PARTS: Record<string, string> = {
  metrics: 'metrics',
  levers: 'levers',
  roadmaps: 'roadmaps',
  roadmapLevers: 'levers in one roadmap',
  analytics: 'definition values',
  analyticsAreas: 'definition areas',
};

const EXPECTED_VALUE: Record<string, string> = {
  integer: 'an integer',
  long: 'a long integer',
  number: 'a number',
  boolean: 'TRUE or FALSE',
  date: 'a date',
};

const MESSAGES: Record<string, (a: Args) => string> = {
  // ------------------------------------------------------ raw metric value
  'value.plain': (a) => a.value ?? '',
  'value.noData': () => 'No data available',
  'value.scaFlaws': (a) =>
    a.count === '0'
      ? `No open ${a.severity} flaws`
      : `${num(a.count)} open ${a.severity} ${plural(a.count, 'flaw')}`,
  'value.mfa': (a) => (a.users ? `${a.state} · ${num(a.users)} ${plural(a.users, 'user')}` : `${a.state} · user count unknown`),
  'value.monitoring': (a) => a.capabilities ?? '',
  'value.monitoringNone': () => 'No monitoring',
  'value.compositeProvided': (a) => `${a.value} (value provided by ${a.system})`,
  'value.compositeCounts': (a) => `${a.value} (${counts(a.counts)})`,

  // ------------------------------------------------------ metric exclusion
  'exclusion.notReported': (a) => (a.system ? `Not reported by ${a.system}` : 'Not reported'),
  'exclusion.scaSeverityNotReported': (a) => `The SCA scan did not report ${a.severity} severity`,
  'exclusion.containerizedNo': () => "Not containerized: the BRD assigns no score to 'No'",
  'exclusion.monitoringNotReported': () => 'No monitoring capability was reported',
  'exclusion.compositeNoProducts': (a) => `The application has no products reported by ${a.system}`,
  'exclusion.zeroValue': (a) =>
    a.system
      ? `${a.system} reported 0, which the BRD excludes from scoring like a missing value`
      : 'Reported as 0, which the BRD excludes from scoring like a missing value',
  'exclusion.notCritical': () => 'Only scored for critical applications, and this one is not critical',

  // ---------------------------------------------------------------- what-if
  'lever.compositeProvided': (a) =>
    `${a.system} delivers the composite value already calculated, without the per-product detail: there are no counts to reclassify.`,
  'lever.notCritical': () => 'This metric only counts for critical applications, and this one is not critical: moving it changes nothing.',
  'scenario.weightAdded': (a) =>
    `The combined result is not the sum of the parts: these levers bring data to metrics that had none and raise the calculation weight from ${num(a.from)} to ${num(a.to)}. Adding up the individual deltas would give ${score(a.sum)} instead of ${score(a.delta)}.`,
  'scenario.interaction': (a) =>
    `The combined result differs from the sum of the parts by ${score(a.effect)} points.`,
  'target.alreadyMet': (a) => `The application already exceeds the target: its score is ${score(a.score)}.`,
  'target.unreachable': (a) =>
    a.projected
      ? `A target of ${score(a.target)} cannot be reached. The ${num(a.count)} ${plural(a.count, 'lever')} that add points take the score from ${score(a.baseline)} to ${score(a.projected)}, as close as it gets; the ceiling with every lever at its maximum is ${score(a.ceiling)}.`
      : `A target of ${score(a.target)} is above this application's reachable ceiling, which is ${score(a.ceiling)} even with every lever applied.`,
  'target.reachable': (a) =>
    `With ${num(a.count)} ${plural(a.count, 'lever')} the score goes from ${score(a.baseline)} to ${score(a.projected)}.`,

  // ---------------------------------------------------------------- roadmap
  'alert.roadmapMissing': (a) => `Disposition ${a.disposition} without an assigned roadmap.`,
  'alert.pairInvalid': (a) => `${a.disposition} with ${a.roadmap}: that pair does not exist in MAPS.`,
  'alert.investProducts': (a) =>
    `${num(a.count)} ${plural(a.count, 'product')} classified as Invest in an application marked to leave.`,
  'alert.manyUsers': (a) => `${num(a.users)} registered users.`,
  'alert.criticalFlaws': (a) =>
    `${num(a.count)} open Critical-severity SCA ${plural(a.count, 'flaw')}.`,
  'alert.usersWithoutMfa': (a) =>
    `${num(a.users)} ${plural(a.users, 'user')} without multi-factor; the rehost leaves them unchanged.`,
  'alert.healthyScore': (a) => `Current score ${score(a.score)}, above ${num(a.threshold)}.`,
  'alert.criticalWithoutDr': () => 'Critical Application Indicator = Y, DR In-Scope = No.',
  'outlook.noRoadmap': () => 'No roadmap assigned: this application has no future plan to project.',
  'outlook.leavesPortfolio': (a) =>
    `${a.roadmap} takes the application out of the portfolio in about ${num(a.weeks)} ${plural(a.weeks, 'week')}. Its score does not improve because it stops existing: investing to raise it would mean spending on something about to be switched off.`,
  'outlook.noScoreEffect': (a) =>
    `${a.roadmap} does not move any of the score's metrics. The roadmap's value lies elsewhere.`,
  'outlook.projected': (a) =>
    `Executing ${a.roadmap} takes the score from ${score(a.baseline)} to ${score(a.projected)}, ${signed(a.delta)} points, in about ${num(a.weeks)} ${plural(a.weeks, 'week')}.`,
  'outlook.automaticPlan': (a) =>
    `No MAPS roadmap: an automatic plan with ${num(a.levers)} ${plural(a.levers, 'lever')} takes the score from ${score(a.baseline)} to ${score(a.projected)}, ${signed(a.delta)} points, reaching the target of ${score(a.target)}.`,
  'outlook.automaticPlanShort': (a) =>
    `No MAPS roadmap: the automatic target of ${score(a.target)} cannot be reached. With the ${num(a.levers)} ${plural(a.levers, 'lever')} that add points, the score goes from ${score(a.baseline)} to ${score(a.projected)}, ${signed(a.delta)} points.`,
  // ------------------------------------------------------ future plan, paso a paso
  'plan.mfa': (a) =>
    a.users
      ? `Integrate the sign-in of its ${num(a.users)} ${plural(a.users, 'user')} with the corporate multi-factor provider.`
      : 'Integrate the sign-in with the corporate multi-factor provider.',
  'plan.dr': (a) =>
    a.rto || a.rpo
      ? `Bring the application into the disaster recovery plan and test a recovery within its declared objectives: RTO ${a.rto ?? 'not declared'}, RPO ${a.rpo ?? 'not declared'}.`
      : 'Bring the application into the disaster recovery plan, declare its RTO and RPO, and test a recovery.',
  'plan.sca.remediate': (a) =>
    `Close the ${num(a.count)} open ${a.severity}-severity SCA ${plural(a.count, 'finding')}: upgrade or replace the affected libraries and scan again.`,
  'plan.sca.finding': (a) =>
    `${a.library ?? 'Unnamed library'}${a.vendor ? ` (${a.vendor})` : ''}: ${a.cve ?? a.vulnerability ?? 'vulnerability without a CVE'}` +
    (a.cvss ? `, CVSS ${a.cvss}` : '') +
    (a.age ? `, open for ${num(a.age)} ${plural(a.age, 'day')}` : '') +
    '.',
  'plan.sca.more': (a) =>
    `And ${num(a.more)} more ${a.severity}-severity ${plural(a.more, 'finding')} in the Veracode report.`,
  'plan.kenna': (a) =>
    `Remediate the host vulnerabilities reported by Qualys until the Kenna score goes from ${num(a.from)} to ${num(a.to)} or less.`,
  'plan.oss': (a) =>
    `Replace or upgrade the lowest-rated open source components until the Open-Source Score goes from ${score(a.from)} to ${score(a.to)}.`,
  'plan.rawTech': (a) =>
    `Upgrade the components near or past their end of life until the CMT Raw Tech Score goes from ${score(a.from)} to ${score(a.to)}.`,
  'plan.rawTech.components': (a) =>
    `CMT counts ${num(a.obsolete)} obsolete and ${num(a.notApproved)} not-approved components in this application: start with those.`,
  'plan.api': (a) =>
    a.current
      ? `Expose the services as native REST APIs managed by APIM (today: ${a.current}).`
      : 'Expose the services as native REST APIs managed by APIM (today there is no API exposure on record).',
  'plan.techDebt': (a) =>
    `Close the technical debt items recorded in the inventory: the combined debt score goes from ${score(a.from)} to 0.`,
  'plan.archValue': (a) =>
    `Replace ${num(a.count)} ${plural(a.count, 'product')} with modern, approved equivalents: ${countsText([
      [a.obsolete, 'obsolete'],
      [a.notApproved, 'not approved for use'],
      [a.heritage, 'heritage'],
    ])}.`,
  'plan.productStrategy': (a) =>
    `Reclassify ${num(a.count)} ${plural(a.count, 'product')} to Invest: ${countsText([
      [a.eliminate, 'from Eliminate'],
      [a.migrate, 'from Migrate'],
      [a.tolerate, 'from Tolerate'],
    ])}.`,
  'plan.monitoring': (a) => {
    const others = (a.missing ?? '')
      .split(',')
      .filter((capability) => capability && capability !== 'elastic')
      .map((capability) => MONITORING[capability] ?? capability);

    return others.length > 0
      ? `Deploy the Elastic agent. The application also has no ${listText(others)}.`
      : 'Deploy the Elastic agent.';
  },
  'plan.operational': (a) =>
    `Reduce the incidents and problems reported in ServiceNow until the operational score goes from ${score(a.from)} to ${score(a.to)}.`,
  'plan.generic': (a) => `${a.action ?? ''}.`,

  'outlook.automaticPlanMet': (a) =>
    `No MAPS roadmap, and the score of ${score(a.baseline)} already reaches the automatic target of ${score(a.target)}: there is nothing to plan.`,

  // ---------------------------------------------------------------- formula
  'formula.numerator': (a) => `Numerator: sum of the weighted scores with data = ${a.numerator}`,
  'formula.denominator': (a) => `Denominator: sum of the weights with data = ${a.denominator} of 100`,
  'formula.excluded': (a) =>
    `${num(a.count)} ${plural(a.count, 'metric')} without data ${a.count === '1' ? 'is' : 'are'} left out of the numerator and the denominator`,
  'formula.quotient': (a) => `Quotient: ${a.numerator} / ${a.denominator} = ${a.quotient}`,
  'formula.normalization': (a) =>
    `Normalization: ${a.floor} + ${a.quotient} × ${a.range} = ${score(a.score)}`,

  // ----------------------------------------------------------------- upload
  'upload.noFiles': () => 'No file was received.',
  'upload.tooLarge': (a) => `The upload weighs ${a.size} MB and the maximum is ${a.max} MB.`,
  'upload.busy': () => 'Another upload is being published right now. Try again when it finishes.',
  'upload.missingMaster': (a) =>
    `${a.master} is missing. It is the application inventory: without it there is no portfolio to score.`,
  'upload.empty': () =>
    'The upload produced no applications. Replacing the portfolio with an empty one would be worse than not uploading at all, so it was discarded.',
  'upload.tooManyFiles': (a) => `An upload accepts up to ${a.max} files; ${a.count} were received.`,
  'upload.assignmentMisaligned': () => 'The source assignment does not line up with the files that were sent.',
  'upload.notSupported': () => 'The active data adapter does not accept uploads.',
  'upload.labelInvalid': (a) =>
    `The label can be up to ${a.max} characters long and cannot contain line breaks or control characters.`,
  'upload.disabledAdapter': (a) =>
    `The active adapter is '${a.provider}', which serves generated data. Uploading files requires the file or database adapter.`,
  'upload.disabledPermission': (a) =>
    `Your user can view uploads but not publish them. Publishing requires the '${a.role}' role and scope over the whole portfolio.`,
  'upload.published': (a) =>
    `Upload published: ${num(a.files)} ${plural(a.files, 'file')}, ${num(a.rows)} ${plural(a.rows, 'row')}, ${num(a.applications)} ${plural(a.applications, 'application')} recalculated.` +
    (a.rejectedRows && a.rejectedRows !== '0'
      ? ` ${num(a.rejectedRows)} ${plural(a.rejectedRows, 'row was', 'rows were')} left out; see the per-file detail.`
      : ''),
  'upload.rejected': (a) =>
    a.count === '1'
      ? 'The upload was rejected. Nothing in the current portfolio changed.'
      : `The upload was rejected for ${num(a.count)} problems. Nothing in the current portfolio changed.`,
  'upload.rejectedUnidentified': (a) =>
    `The upload was rejected: ${num(a.count)} ${plural(a.count, 'file')} could not be identified by ${a.count === '1' ? 'its' : 'their'} header. Assign the source below and upload again.`,

  // ------------------------------------------------- database sources (ADR 0037)
  'connector.disabled': () =>
    'Reading sources from databases is not available: it needs database connections configured in the deployment and a data adapter that accepts uploads.',
  'connector.disabledPermission': (a) =>
    `Reading sources from databases publishes an upload: it requires the '${a.role}' role and scope over the whole portfolio.`,
  'connector.busy': () => 'The databases are being read right now. Try again when that pull finishes.',
  'connector.providerUnknown': (a) =>
    `The connection ${a.connection} names the engine '${a.provider}', which is not supported. Use SqlServer, PostgreSql, Oracle or Odbc.`,
  'connector.tlsRequired': (a) =>
    `The connection ${a.connection} (${a.provider}) does not use verified TLS, so it was not opened. The deployment guide explains the setting for each engine.`,
  'connector.secretMissing': (a) =>
    `The secret of the connection ${a.connection} is missing or cannot be read. Check the secret mounted from the vault.`,
  'connector.connectionStringInvalid': (a) =>
    `The connection string of ${a.connection} is not valid for its engine.`,
  'connector.authenticationInteractive': (a) =>
    `The connection ${a.connection} uses a sign-in that needs a person. Use a SQL login, Windows integrated security, or a managed identity or service principal.`,
  'connector.queryInvalid': (a) =>
    `The query of ${a.source} is not a single read-only SELECT on a view or table, so it was not run.`,
  'connector.connectFailed': (a) =>
    `Could not connect with ${a.connection}. The server log of the System layer has the database's own message.`,
  'connector.queryFailed': (a) =>
    `The query of ${a.source} failed on ${a.connection}. The server log of the System layer has the database's own message.`,
  'connector.timeout': (a) =>
    a.source
      ? `Reading ${a.source} from ${a.connection} took longer than allowed.`
      : `Connecting with ${a.connection} took longer than allowed.`,
  'connector.tooLarge': (a) =>
    `${a.source} exceeds the limits of an upload (${num(a.rows)} rows or ${a.megabytes} MB). Filter it in the view or the query.`,
  'connector.columnUnnamed': (a) =>
    `Column ${a.position} of ${a.source} has no name. Give every column a name with AS.`,
  'connector.columnUnsupported': (a) =>
    `The column ${a.column} of ${a.source} has a type a source cannot hold (${a.type}). Convert it to text or a number in the query.`,
  'connector.pullFailed': (a) =>
    `${num(a.count)} of ${num(a.total)} ${plural(a.total, 'source')} could not be read, so nothing was published. Nothing in the current portfolio changed.`,

  'file.tooLarge': (a) => `It weighs ${a.size} MB and the per-file maximum is ${a.max} MB.`,
  'file.identical': (a) =>
    `It is identical to ${a.other}: the same file was selected twice, and loading both would count its rows twice. Remove one of them.`,
  'file.noName': () => 'A file without a name was received.',
  'file.extension': (a) => `Only ${a.extension} files are accepted.`,
  'file.contentType': (a) => `The content type '${a.contentType}' is not accepted.`,
  'file.assignmentShape': () => 'The assigned source is not shaped like a file name.',

  'match.assignmentUnknown': () => 'The assigned source does not exist in the catalog.',
  'match.ambiguous': () => 'Its header matches more than one source. Assign it manually before publishing.',
  'match.unrecognized': () => 'Its header does not match any known source.',

  'csv.noHeader': () => 'The file has no header.',
  'csv.fieldTooLong': (a) =>
    a.column
      ? `The ${a.column} cell in row ${a.row} is longer than ${num(a.max)} characters: check that cell in the file. An unclosed quote makes it swallow the rows below it.`
      : `A field in row ${a.row} is longer than ${num(a.max)} characters: the file is corrupt or is not the expected export.`,
  'csv.unreadable': (a) => (a.row ? `The file could not be read as CSV (row ${a.row}).` : 'The file could not be read as CSV.'),
  'csv.tooManyRows': (a) => `The file exceeds the limit of ${num(a.max)} rows.`,
  'csv.missingColumns': (a) => `Required columns are missing: ${a.missing}. The file has: ${a.found}.`,
  'contract.unmapped': (a) => `The '${a.file}' contract has no database mapping.`,

  'xlsx.unreadable': () =>
    'The file could not be read as an Excel workbook. Open it in Excel and save it again as .xlsx, or as CSV.',
  'xlsx.protected': () =>
    'The workbook is password protected or in the old .xls format. Save it as .xlsx without a password, or as CSV.',
  'xlsx.noSheet': () => 'The workbook has no sheet with data: the first visible sheet is the one that is read.',
  'xlsx.tooLarge': () =>
    'The workbook is too large to read safely. Save it as CSV, or split it into several files with the same columns: they are loaded together.',
  'xlsx.tooManyColumns': (a) => `The first row of the sheet has more than ${num(a.max)} columns.`,

  'row.missingKey': (a) => `No value in the key column '${a.column}'.`,
  'row.invalidValue': (a) =>
    `'${a.column}' expected ${EXPECTED_VALUE[a.expected ?? ''] ?? 'a known type'} and received '${a.value}'.` +
    (a.more ? ` (+${a.more} more in this row)` : ''),
  'row.notConvertible': () => 'The row could not be converted to the canonical model.',
  'row.unknownEai': (a) => `EAI '${a.key}' does not exist in the EAI master.`,
  'row.duplicateKey': (a) =>
    `'${a.column}' ${a.key} appears more than once; the last row was kept and this one was left out.`,
  'row.duplicateKeyInFile': (a) =>
    `'${a.column}' ${a.key} appears again in ${a.file}, a later file of the same source; the row from that file was kept and this one was left out.`,

  'mapping.invalid': () => 'The column mapping sent with this file is not valid. Review it again and retry.',
  'mapping.needsSource': () => 'A column mapping belongs to one source: choose the source of this file first.',
  'mapping.unknownColumn': (a) => `'${a.column}' is not a field of this source.`,
  'mapping.headerNotInFile': (a) => `'${a.column}' is mapped to '${a.header}', which is not a column of this file.`,
  'mapping.headerTwice': (a) => `The column '${a.header}' is mapped to more than one field.`,
  'mapping.requiredExcluded': (a) => `'${a.column}' is required: choose the column of the file that holds it.`,
  'mapping.headerMultiline': () =>
    'The header of this file spans more than one line, so its columns cannot be renamed. Export it again without line breaks in the column names.',
  'mapping.headerTooLarge': (a) =>
    `The header has more than ${num(a.max)} columns or is longer than ${num(a.maxLength)} characters.`,

  // ----------------------------------------------------- rules administration
  'rules.metricUnknown': (a) => `Metric #${a.position} does not exist in the ruleset.`,
  'rules.duplicate': (a) => `${a.item} appears more than once in the draft.`,
  'rules.kindMismatch': (a) => `${a.metric} does not use that kind of mapping.`,
  'rules.weightInvalid': (a) =>
    `The weight of ${a.metric} must be between 0 and 100, with up to two decimals.`,
  'rules.weightTotal': (a) =>
    `The weights add up to ${num(a.total)}; they must add up to ${num(a.expected)}.`,
  'rules.scoreInvalid': (a) =>
    `A score of ${a.metric} (${a.item}) must be between 0 and 100, with up to two decimals.`,
  'rules.bandCount': (a) => `${a.metric} needs between 1 and ${a.max} bands.`,
  'rules.bandBounds': (a) =>
    `Band ${a.band} of ${a.metric} has its minimum above its maximum, or a bound out of range.`,
  'rules.bandOrder': (a) =>
    `The bands of ${a.metric} must go from lowest to highest without overlapping.`,
  'rules.bandLabel': (a) =>
    `Band ${a.band} of ${a.metric} needs a label of up to ${a.max} characters.`,
  'rules.caseKeys': (a) =>
    `The cases of ${a.metric} cannot be added or removed; only their scores can change.`,
  'rules.categoryKeys': (a) =>
    `The categories of ${a.metric} cannot be added or removed; only their scores can change.`,
  'rules.leverUnknown': (a) => `Lever #${a.position} does not exist in the catalog.`,
  'rules.leverParameter': (a) =>
    `The default quantity of ${a.lever} must be between 1 and ${a.max}, and only levers that move products have one.`,
  'rules.roadmapUnknown': (a) => `Roadmap #${a.position} does not exist in MAPS.`,
  'rules.roadmapLever': (a) =>
    a.lever
      ? `${a.roadmap} delivers ${a.lever}, which is turned off. Remove it from the roadmap or turn it back on.`
      : `${a.roadmap} delivers a lever that does not exist or is turned off.`,
  'rules.roadmapLeaves': (a) =>
    `${a.roadmap} takes the application out of the portfolio, so it cannot deliver levers.`,
  'rules.milestones': (a) =>
    `${a.roadmap} needs a duration for each of its ${a.count} milestones, between 1 and ${a.max} weeks.`,
  'rules.healthyScore': () => 'The healthy score must be between 20 and 100, with up to two decimals.',
  'rules.userBase': (a) => `The significant user base must be between 0 and ${num(a.max)}.`,
  'rules.tooLarge': (a) =>
    `The draft has too many ${DRAFT_PARTS[a.part ?? ''] ?? 'items'}: the limit is ${num(a.max)}.`,
  'rules.reasonInvalid': (a) => `Explain the change in one line of ${a.min} to ${a.max} characters.`,
  'rules.noChanges': () => 'The draft does not change anything in the active rules.',
  'rules.stale': (a) =>
    `Rules ${a.active} were published while you were editing. Reload to start from them.`,
  'rules.busy': () => 'Another rules publication is in progress. Try again in a moment.',
  'rules.invalid': () => 'The resulting rules are not consistent. Review the draft.',
  'rules.storeUnavailable': () =>
    'The rules store did not respond, so nothing was published. Scores keep using the active rules. Try again in a moment.',
  'rules.analyticsUnknown': (a) => `Definition value #${a.position} does not exist in the catalog.`,
  'rules.analyticsValue': (a) =>
    `${parameterLabel(a.parameter ?? '')} must be between ${num(a.min)} and ${num(a.max)}` +
    (a.decimals === '0' ? ', a whole number.' : `, with up to ${a.decimals} decimals.`),
  'rules.analyticsInconsistent': (a) => constraintText(a.rule ?? ''),
  'rules.analyticsArea': (a) => `Definition area #${a.position} does not exist.`,
  'rules.analyticsStatus': (a) => `${areaName(a.area ?? '')}: the status must be provisional or agreed.`,
  'rules.analyticsOwner': (a) =>
    `${areaName(a.area ?? '')}: the owner is one line of up to ${a.max} characters, without control characters.`,

  'change.weight': (a) => `${a.metric}: weight ${num(a.from)} → ${num(a.to)}`,
  'change.bands': (a) => `${a.metric}: bands ${a.from} → ${a.to}`,
  'change.case': (a) => `${a.metric}, “${a.case}”: score ${num(a.from)} → ${num(a.to)}`,
  'change.category': (a) =>
    `${a.metric}, ${COMPOSITE_CATEGORIES[a.category ?? ''] ?? a.category}: score ${num(a.from)} → ${num(a.to)}`,
  'change.leverEnabled': (a) => `${a.lever}: ${a.enabled === 'true' ? 'turned on' : 'turned off'}`,
  'change.leverParameter': (a) => `${a.lever}: default quantity ${num(a.from)} → ${num(a.to)}`,
  'change.roadmapLevers': (a) =>
    `${a.roadmap}: ` +
    [a.added ? `now delivers ${a.added}` : '', a.removed ? `no longer delivers ${a.removed}` : '']
      .filter(Boolean)
      .join('; '),
  'change.milestoneWeeks': (a) =>
    `${a.roadmap}, “${a.milestone}”: ${num(a.from)} → ${num(a.to)} ${plural(a.to, 'week')}`,
  'change.healthyScore': (a) => `Healthy score: ${num(a.from)} → ${num(a.to)}`,
  'change.userBase': (a) => `Significant user base: ${num(a.from)} → ${num(a.to)} users`,
  'change.analytics': (a) => `${parameterLabel(a.parameter ?? '')}: ${num(a.from)} → ${num(a.to)}`,
  'change.analyticsStatus': (a) =>
    `${areaName(a.area ?? '')}: ${statusLabel(a.from ?? '').toLowerCase()} → ${statusLabel(a.to ?? '').toLowerCase()}`,
  'change.analyticsOwner': (a) =>
    `${areaName(a.area ?? '')}: owner ${a.from ? `“${a.from}”` : 'none'} → ${a.to ? `“${a.to}”` : 'none'}`,

};

/** Codigos que llevan el archivo en la propia frase y no necesitan prefijo. */
const FILE_IN_SENTENCE = new Set(['contract.unmapped', 'row.duplicateKeyInFile']);

/**
 * La frase de un aviso. Un codigo que este catalogo no conoce no rompe la pantalla:
 * se muestra el codigo, que es lo mas util para quien lo reporte.
 */
export function formatNotice(notice: Notice | null | undefined): string {
  if (!notice) return '';

  const message = MESSAGES[notice.code];
  const args = notice.args ?? {};
  const text = message ? message(args) : `Unrecognized message (${notice.code})`;

  return args.file && !FILE_IN_SENTENCE.has(notice.code) ? `${args.file}: ${text}` : text;
}

/** "2 obsolete, 1 heritage": las cantidades que no son cero, con su nombre. */
function countsText(parts: readonly (readonly [string | undefined, string])[]): string {
  const present = parts.filter(([count]) => Number(count) > 0).map(([count, label]) => `${num(count)} ${label}`);
  return present.length > 0 ? present.join(', ') : 'none listed';
}

/** "APM, synthetic monitoring and general monitoring". */
function listText(items: readonly string[]): string {
  if (items.length <= 1) return items[0] ?? '';
  return `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

const MONITORING: Record<string, string> = {
  apm: 'APM',
  synthetic: 'synthetic monitoring',
  general: 'general monitoring',
};

// ------------------------------------------------------ upload rejections

/**
 * Qué le pasa a un grupo de filas rechazadas, en una frase: la columna, qué esperaba y
 * el valor de la primera fila que lo tuvo. Es el mismo aviso de linaje, redactado para
 * un grupo y no para una fila.
 */
export function rejectionReasonText(reason: Notice, firstRow: number | undefined): string {
  const a = reason.args ?? {};

  if (reason.code === 'row.invalidValue') {
    const expected = EXPECTED_VALUE[a.expected ?? ''] ?? 'a known type';
    const where = firstRow === undefined ? '' : ` in row ${numberFormat.format(firstRow)}`;
    return `'${a.column}' expected ${expected}. For example, '${a.value}'${where}.`;
  }

  return formatNotice(reason);
}

/**
 * Una pista cuando el valor delata la causa. Solo para casos que se reconocen sin
 * adivinar; si no, nada.
 */
export function rejectionHint(reason: Notice): string | null {
  const a = reason.args ?? {};

  if (reason.code === 'row.missingKey') {
    return 'Rows without the key are usually totals, subtotals or separator lines exported with the data.';
  }

  if (reason.code === 'row.duplicateKey') {
    return 'An export with one row per date (Year, Month, Day) repeats each application. The last row of each one is kept: export only the latest date, or sort the export from oldest to newest.';
  }

  if (reason.code === 'row.duplicateKeyInFile') {
    return 'Files of the same source are loaded together, in the order they were selected. When an application is in more than one, the row of the last file is the one loaded.';
  }

  if (reason.code !== 'row.invalidValue') return null;

  // Los marcadores de «sin dato» (N/A, -, (En blanco)…) ya se leen como vacíos y no
  // llegan aquí (ADR 0028).
  const value = (a.value ?? '').trim();

  if ((a.expected === 'integer' || a.expected === 'long') && /^\d{1,3}(\.\d{3})+$/.test(value)) {
    return `If '${value}' means ${value.replaceAll('.', ',')}, the export uses a dot as thousands separator. Export the numbers without thousands separators.`;
  }

  if ((a.expected === 'integer' || a.expected === 'long') && /^-?\d+[.,]\d*[1-9]\d*$/.test(value)) {
    return 'This column takes whole numbers and the file has decimals. Round them in the export, or tell the team if the source really has decimals.';
  }

  if (a.expected === 'boolean') {
    return 'Accepted values: TRUE/FALSE, Yes/No, Y/N, Sí/No or 1/0.';
  }

  if (a.expected === 'date') {
    return 'Accepted formats: dd/MM/yyyy, yyyy-MM-dd or MM/dd/yyyy, with or without time.';
  }

  if (/[a-z]/i.test(value) && (a.expected === 'integer' || a.expected === 'long' || a.expected === 'number')) {
    return 'The column has text where a number is expected. If it happens in every row, the field may be loaded from the wrong column (check it with Review column mapping) or the columns may be shifted, for example by commas inside unquoted values.';
  }

  return null;
}

/** "rows 2, 3 and 4", "rows 2, 3, 4, 5, 6 and 3,864 more". */
export function sampleRowsText(sampleRows: readonly number[], total: number): string {
  if (sampleRows.length === 0) return '';

  const listed = sampleRows.map((row) => numberFormat.format(row));
  const rest = total - sampleRows.length;
  const label = sampleRows.length === 1 && rest === 0 ? 'row' : 'rows';

  if (rest > 0) return `${label} ${listed.join(', ')} and ${numberFormat.format(rest)} more`;
  if (listed.length === 1) return `${label} ${listed[0]}`;
  return `${label} ${listed.slice(0, -1).join(', ')} and ${listed[listed.length - 1]}`;
}

// ----------------------------------------------------------------- counts

/** "1 application", "250 applications": el conteo con su sustantivo concordado. */
export function countOf(count: number, singular: string, many = `${singular}s`): string {
  return `${numberFormat.format(count)} ${count === 1 ? singular : many}`;
}

// ------------------------------------------------------------------ bands

const BANDS: Record<string, string> = {
  critical: 'Critical',
  'at-risk': 'At risk',
  acceptable: 'Acceptable',
  solid: 'Solid',
};

/** Nombre de un tramo de score. El backend solo envia el codigo. */
export function bandLabel(code: string): string {
  return BANDS[code] ?? code;
}

/**
 * Nombres de tramo de la Fase 10. Un enlace guardado entonces todavia puede
 * traerlos; el servidor los acepta, y aqui se vuelven codigo para que la barra
 * de filtros no muestre el nombre anterior.
 */
const LEGACY_BANDS: Record<string, string> = {
  critico: 'critical',
  'en riesgo': 'at-risk',
  aceptable: 'acceptable',
  solido: 'solid',
};

/** El codigo de un tramo, aunque llegue con su nombre anterior. */
export function bandCode(value: string): string {
  return LEGACY_BANDS[value.trim().toLowerCase()] ?? value;
}

/** Nombre de una clave de agregado: la vacia es "sin asignar". */
export function groupLabel(key: string): string {
  return key === '' ? 'Unassigned' : key;
}

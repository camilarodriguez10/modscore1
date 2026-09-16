# ADR 0021 — El cuerpo de reglas se valida por contrato tipado, no por esquema JSON

**Estado:** aceptada · **Fecha:** 2026-09-15 · **Relacionado:** ASVS 13.2.2, SEC-12, [ADR 0018](0018-las-reglas-se-publican-como-versiones.md)

## Contexto

ASVS L2 13.2.2 pide validar las entradas estructuradas contra un esquema. La matriz
marcaba el requisito como **parcial**: hay DTO tipados y comprobaciones explícitas, pero
no un validador de esquema formal (JSON Schema).

El cuerpo que importa es el borrador de reglas: `POST /api/admin/rules/simulate` y
`/publish`. Es el único punto donde un cliente manda una estructura anidada que decide
cómo se calcula el score de todo el portafolio.

Hoy ese cuerpo pasa por tres filtros antes de tocar nada:

1. **`BoundedJson`** (capa Channel y capa System), con un deserializador estricto:
   - tope propio de 128 KB, comprobado por `Content-Length` y mientras se lee;
   - `UnmappedMemberHandling.Disallow`: una propiedad que no existe en el contrato es un
     rechazo, no algo que se ignora en silencio;
   - `RespectNullableAnnotations` y `RespectRequiredConstructorParameters`: un nulo donde
     el contrato no lo admite, o un campo obligatorio ausente, no construyen el objeto;
   - `MaxDepth = 16` contra anidamiento abusivo;
   - sin eco del error del parser, que diría en qué propiedad y con qué valor falló.
2. **El tipo** al que se deserializa: cada valor tiene su tipo de dominio y sus rangos.
3. **`Ruleset.Validate`**: invariantes del modelo de score (pesos, bandas sin huecos,
   estrategias conocidas, `zeroIsNoData` solo en métricas numéricas). Un borrador que no
   valida no se simula ni se publica, y el error vuelve como aviso con código.

## Decisión

**No se agrega un validador de esquema JSON.** El contrato tipado estricto más las
invariantes del dominio cubren lo que pide 13.2.2, y el requisito pasa a **cumple** con
esta decisión registrada.

Pesó en contra de agregar la librería:

- **Cadena de suministro.** Una dependencia más en el árbol del servicio que puntúa el
  portafolio, para revalidar lo que el deserializador estricto ya rechaza.
- **Dos fuentes de verdad.** El esquema y los tipos se separan en cuanto alguien cambia
  uno y olvida el otro. Hoy el contrato es el tipo, y las pruebas lo comparan contra el
  TypeScript de la SPA (`ViewModelContractTests`).
- **Lo que el esquema no ve.** Las reglas de negocio del ruleset —pesos que suman, bandas
  contiguas, estrategias válidas— no son expresables en JSON Schema sin duplicar lógica.

## Consecuencias

- Los cuerpos anidados nuevos deben usar `BoundedJson` con su tope propio, no el enlace
  automático de minimal APIs, que acepta propiedades desconocidas y lee hasta 1 MB.
- Las pruebas de la Fase 12 y 13 cubren el rechazo: cuerpo malformado, propiedad
  desconocida, cuerpo demasiado grande y borrador que no valida.
- Desde la Fase 14, un rechazo por validación deja el evento de auditoría 4214 con la ruta
  y el código, sin los valores enviados (ASVS 7.1.3).

## Cuándo revisar esta decisión

- Si el sistema publica una API para terceros, donde el esquema también es documentación y
  contrato con quien integra.
- Si aparece un cuerpo dinámico, cuyas propiedades no se conocen en tiempo de compilación.
- Si el equipo de seguridad exige esquema formal como control estándar.

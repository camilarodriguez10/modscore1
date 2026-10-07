/**
 * El encabezado de un libro de Excel (.xlsx), leído en el navegador para revisar el mapping.
 *
 * Igual que con un CSV, lo único que viaja es la fila de encabezado: del libro se descomprime
 * solo lo necesario para encontrarla —el directorio del ZIP, el libro, sus relaciones, el
 * principio de la primera hoja visible y los textos compartidos hasta el último que usa el
 * encabezado—. Ninguna fila de datos sale del equipo hasta que se carga, y la conversión
 * completa la hace la capa System (ADR 0034). Sin dependencias: el ZIP lo descomprime el
 * navegador (DecompressionStream) y el XML se recorre con expresiones acotadas.
 */

const END_OF_DIRECTORY = 0x06054b50;
const DIRECTORY_ENTRY = 0x02014b50;
const LOCAL_HEADER = 0x04034b50;

/** Topes: un archivo fabricado no puede hacer que el navegador descomprima sin fin. */
const MAX_DIRECTORY_BYTES = 4 * 1024 * 1024;
const MAX_ENTRIES = 10_000;
const MAX_PART_CHARS = 16 * 1024 * 1024;
const MAX_SHEET_CHARS = 8 * 1024 * 1024;
const MAX_SHARED_CHARS = 64 * 1024 * 1024;
const MAX_COLUMNS = 1_000;

const NS = '(?:[\\w.-]+:)?';

interface ZipEntry {
  method: number;
  compressedSize: number;
  offset: number;
}

interface Relationship {
  id: string;
  type: string;
  target: string;
}

interface HeaderCell {
  column: number;
  type: string | null;
  value: string | null;
  inline: string | null;
}

/** True si el archivo empieza como un ZIP: un .xlsx, se llame como se llame. */
export async function isWorkbook(file: Blob): Promise<boolean> {
  const head = new Uint8Array(await file.slice(0, 4).arrayBuffer());
  return head.length === 4 && head[0] === 0x50 && head[1] === 0x4b && head[2] === 0x03 && head[3] === 0x04;
}

/** La fila de encabezado de la primera hoja visible, como una línea CSV. */
export async function readWorkbookHeaderLine(file: Blob): Promise<string> {
  const entries = await readDirectory(file);

  const rootRels = await readPart(file, entries, '_rels/.rels');
  const workbookPath =
    relationships(rootRels ?? '', '').find((r) => r.type.endsWith('/officeDocument'))?.target ?? 'xl/workbook.xml';

  const workbook = await readPart(file, entries, workbookPath);
  if (workbook === null) throw new Error('The workbook part is missing.');

  const related = relationships((await readPart(file, entries, relationshipsPathOf(workbookPath))) ?? '', workbookPath);
  const sheets = [...workbook.matchAll(new RegExp(`<${NS}sheet\\b([^>]*)/?>`, 'g'))].map((match) => {
    const attributes = match[1] ?? '';
    return {
      id: /\b[\w.-]+:id="([^"]*)"/.exec(attributes)?.[1] ?? null,
      visible: !/\bstate="(hidden|veryHidden)"/.test(attributes),
    };
  });

  const sheet = sheets.find((candidate) => candidate.visible) ?? sheets[0];
  const sheetPath = related.find((r) => r.id === sheet?.id)?.target;
  const sheetEntry = sheetPath ? entries.get(sheetPath.toLowerCase()) : undefined;
  if (!sheetEntry) throw new Error('The workbook has no sheet.');

  const cells = await firstRow(file, sheetEntry);
  if (cells.length === 0) throw new Error('The sheet has no rows.');

  const sharedIndexes = cells.filter((cell) => cell.type === 's').map((cell) => Number(cell.value));
  const sharedPath = related.find((r) => r.type.endsWith('/sharedStrings'))?.target;
  const sharedEntry = sharedPath ? entries.get(sharedPath.toLowerCase()) : undefined;
  const shared =
    sharedIndexes.length > 0 && sharedEntry ? await sharedStrings(file, sharedEntry, Math.max(...sharedIndexes)) : [];

  const header: string[] = [];

  for (const cell of cells) {
    const text = cellText(cell, shared);
    if (text === '') continue;
    if (cell.column >= MAX_COLUMNS) throw new Error('The header has too many columns.');
    while (header.length < cell.column) header.push('');
    header[cell.column] = text;
  }

  return toCsvLine(header);
}

/** Un número como lo escribe la capa System: 15 cifras, sin exponente y sin parecer de miles. */
export function formatNumber(raw: string): string {
  const value = Number(raw.trim());
  if (raw.trim() === '' || !Number.isFinite(value)) return raw.trim();

  let text = String(Number(value.toPrecision(15)));
  if (/e/i.test(text)) text = Number(text).toLocaleString('en-US', { useGrouping: false, maximumFractionDigits: 20 });

  return /^-?\d{1,3}\.\d{3}$/.test(text) ? `${text}0` : text;
}

/** Una línea CSV con comillas donde hacen falta, como la escribe la capa System. */
export function toCsvLine(fields: readonly string[]): string {
  return fields
    .map((field) =>
      /[",\r\n]/.test(field) || /^\s|\s$/.test(field) ? `"${field.replaceAll('"', '""')}"` : field,
    )
    .join(',');
}

// ------------------------------------------------------------------ ZIP

async function readDirectory(file: Blob): Promise<Map<string, ZipEntry>> {
  const tailLength = Math.min(file.size, 22 + 0xffff);
  const tail = new DataView(await file.slice(file.size - tailLength).arrayBuffer());
  let end = -1;

  for (let i = tailLength - 22; i >= 0; i--) {
    if (tail.getUint32(i, true) === END_OF_DIRECTORY) {
      end = i;
      break;
    }
  }

  if (end < 0) throw new Error('Not a ZIP file.');

  const count = tail.getUint16(end + 10, true);
  const size = tail.getUint32(end + 12, true);
  const offset = tail.getUint32(end + 16, true);

  if (count > MAX_ENTRIES || size > MAX_DIRECTORY_BYTES || offset + size > file.size) {
    throw new Error('The ZIP directory is not usable.');
  }

  const directory = new DataView(await file.slice(offset, offset + size).arrayBuffer());
  const names = new TextDecoder('utf-8');
  const entries = new Map<string, ZipEntry>();
  let position = 0;

  for (let i = 0; i < count; i++) {
    if (position + 46 > size || directory.getUint32(position, true) !== DIRECTORY_ENTRY) {
      throw new Error('The ZIP directory is corrupt.');
    }

    const method = directory.getUint16(position + 10, true);
    const compressedSize = directory.getUint32(position + 20, true);
    const nameLength = directory.getUint16(position + 28, true);
    const extraLength = directory.getUint16(position + 30, true);
    const commentLength = directory.getUint16(position + 32, true);
    const localOffset = directory.getUint32(position + 42, true);
    const name = names.decode(new Uint8Array(directory.buffer, position + 46, nameLength));

    entries.set(name.replaceAll('\\', '/').replace(/^\//, '').toLowerCase(), {
      method,
      compressedSize,
      offset: localOffset,
    });

    position += 46 + nameLength + extraLength + commentLength;
  }

  return entries;
}

async function entryStream(file: Blob, entry: ZipEntry): Promise<ReadableStream<Uint8Array>> {
  if (entry.compressedSize === 0xffffffff || entry.offset === 0xffffffff) {
    throw new Error('ZIP64 workbooks are not read in the browser.');
  }

  const local = new DataView(await file.slice(entry.offset, entry.offset + 30).arrayBuffer());
  if (local.byteLength < 30 || local.getUint32(0, true) !== LOCAL_HEADER) throw new Error('Corrupt ZIP entry.');

  const start = entry.offset + 30 + local.getUint16(26, true) + local.getUint16(28, true);
  const data = file.slice(start, start + entry.compressedSize).stream();

  if (entry.method === 0) return data;
  if (entry.method === 8) return data.pipeThrough(new DecompressionStream('deflate-raw'));
  throw new Error('Unsupported ZIP compression.');
}

/** Lee el texto de una parte hasta que `done` diga que basta, sin pasar del tope. */
async function readText(
  file: Blob,
  entry: ZipEntry,
  maxChars: number,
  done: (text: string) => boolean = () => false,
): Promise<string> {
  const reader = (await entryStream(file, entry)).getReader();
  const decoder = new TextDecoder('utf-8');
  let text = '';

  try {
    for (;;) {
      const { done: finished, value } = await reader.read();
      if (finished) break;

      text += decoder.decode(value, { stream: true });
      if (done(text)) return text;
      if (text.length > maxChars) throw new Error('The workbook part is too large to read here.');
    }

    return text + decoder.decode();
  } finally {
    await reader.cancel().catch(() => undefined);
  }
}

async function readPart(file: Blob, entries: Map<string, ZipEntry>, path: string): Promise<string | null> {
  const entry = entries.get(path.toLowerCase());
  return entry ? readText(file, entry, MAX_PART_CHARS) : null;
}

// ------------------------------------------------------------------ partes del libro

function relationships(xml: string, sourcePart: string): Relationship[] {
  return [...xml.matchAll(new RegExp(`<${NS}Relationship\\b([^>]*)/?>`, 'g'))]
    .map((match) => {
      const attributes = match[1] ?? '';
      const attribute = (name: string) => new RegExp(`\\b${name}="([^"]*)"`).exec(attributes)?.[1];
      const external = attribute('TargetMode')?.toLowerCase() === 'external';
      const id = attribute('Id');
      const type = attribute('Type');
      const target = attribute('Target');

      return !external && id && type && target ? { id, type, target: resolve(sourcePart, decode(target)) } : null;
    })
    .filter((relationship): relationship is Relationship => relationship !== null);
}

function relationshipsPathOf(part: string): string {
  const slash = part.lastIndexOf('/');
  return slash < 0 ? `_rels/${part}.rels` : `${part.slice(0, slash)}/_rels/${part.slice(slash + 1)}.rels`;
}

/** El destino de una relación como ruta dentro del ZIP, sin subir de la raíz. */
function resolve(sourcePart: string, target: string): string {
  const base = target.startsWith('/') ? '' : sourcePart.slice(0, sourcePart.lastIndexOf('/') + 1);
  const segments: string[] = [];

  for (const segment of `${base}${target}`.replaceAll('\\', '/').split('/')) {
    if (segment === '' || segment === '.') continue;
    if (segment === '..') segments.pop();
    else segments.push(segment);
  }

  return segments.join('/');
}

/** La primera fila con algún valor. Deja de descomprimir apenas la encuentra. */
async function firstRow(file: Blob, entry: ZipEntry): Promise<HeaderCell[]> {
  const rowPattern = new RegExp(`<${NS}row\\b[^>]*?(?:/>|>([\\s\\S]*?)</${NS}row>)`, 'g');
  let found: HeaderCell[] | null = null;
  let scanned = 0;

  const text = await readText(file, entry, MAX_SHEET_CHARS, (current) => {
    rowPattern.lastIndex = scanned;

    for (let match = rowPattern.exec(current); match; match = rowPattern.exec(current)) {
      scanned = rowPattern.lastIndex;
      const cells = rowCells(match[1] ?? '');

      if (cells.some((cell) => (cell.value ?? cell.inline ?? '') !== '')) {
        found = cells;
        return true;
      }
    }

    return false;
  });

  if (found) return found;

  // La hoja entera cupo en un solo bloque: se busca una última vez.
  rowPattern.lastIndex = 0;
  for (let match = rowPattern.exec(text); match; match = rowPattern.exec(text)) {
    const cells = rowCells(match[1] ?? '');
    if (cells.some((cell) => (cell.value ?? cell.inline ?? '') !== '')) return cells;
  }

  return [];
}

function rowCells(row: string): HeaderCell[] {
  const cells: HeaderCell[] = [];
  let next = 0;

  for (const match of row.matchAll(new RegExp(`<${NS}c\\b([^>]*?)(?:/>|>([\\s\\S]*?)</${NS}c>)`, 'g'))) {
    const attributes = match[1] ?? '';
    const content = match[2] ?? '';
    const reference = /\br="([A-Za-z]{1,3})\d*"/.exec(attributes)?.[1];
    const column = reference ? columnIndex(reference) : next;
    next = column + 1;

    const inlineMatch = new RegExp(`<${NS}is\\b[^>]*>([\\s\\S]*?)</${NS}is>`).exec(content);

    cells.push({
      column,
      type: /\bt="([^"]*)"/.exec(attributes)?.[1] ?? null,
      value: new RegExp(`<${NS}v>([\\s\\S]*?)</${NS}v>`).exec(content)?.[1] ?? null,
      inline: inlineMatch ? richText(inlineMatch[1] ?? '') : null,
    });
  }

  return cells;
}

/** Los textos compartidos hasta el índice `last`; no descomprime más. */
async function sharedStrings(file: Blob, entry: ZipEntry, last: number): Promise<string[]> {
  const itemPattern = new RegExp(`<${NS}si\\b[^>]*?(?:/>|>([\\s\\S]*?)</${NS}si>)`, 'g');
  const strings: string[] = [];
  let scanned = 0;

  const collect = (text: string) => {
    itemPattern.lastIndex = scanned;

    for (let match = itemPattern.exec(text); match; match = itemPattern.exec(text)) {
      scanned = itemPattern.lastIndex;
      strings.push(richText(match[1] ?? ''));
      if (strings.length > last) return true;
    }

    return false;
  };

  const text = await readText(file, entry, MAX_SHARED_CHARS, collect);
  if (strings.length <= last) collect(text);

  return strings;
}

/** El texto de un elemento con texto enriquecido, sin la guía fonética. */
function richText(xml: string): string {
  const withoutPhonetic = xml.replace(new RegExp(`<${NS}rPh\\b[\\s\\S]*?</${NS}rPh>`, 'g'), '');
  const parts = [...withoutPhonetic.matchAll(new RegExp(`<${NS}t\\b[^>]*?(?:/>|>([\\s\\S]*?)</${NS}t>)`, 'g'))];

  return unescapeExcel(parts.map((part) => decode(part[1] ?? '')).join(''));
}

function cellText(cell: HeaderCell, shared: readonly string[]): string {
  switch (cell.type) {
    case 's':
      return shared[Number(cell.value)] ?? '';
    case 'inlineStr':
      return cell.inline ?? '';
    case 'str':
      return unescapeExcel(decode(cell.value ?? ''));
    case 'b':
      return cell.value === null ? '' : cell.value.trim() === '1' ? 'TRUE' : 'FALSE';
    case 'e':
      return '';
    case 'd':
      return decode(cell.value ?? '').trim();
    default:
      return cell.value === null ? '' : formatNumber(decode(cell.value));
  }
}

function columnIndex(letters: string): number {
  let column = 0;
  for (const letter of letters.toUpperCase()) column = column * 26 + (letter.charCodeAt(0) - 64);
  return column - 1;
}

/** Las entidades del XML. Las de un DTD no existen aquí: un libro no las usa. */
function decode(text: string): string {
  return text.replace(/&(#x[0-9a-f]+|#\d+|lt|gt|amp|quot|apos);/gi, (_, entity: string) => {
    switch (entity.toLowerCase()) {
      case 'lt':
        return '<';
      case 'gt':
        return '>';
      case 'amp':
        return '&';
      case 'quot':
        return '"';
      case 'apos':
        return "'";
      default: {
        const code = entity[1] === 'x' || entity[1] === 'X' ? parseInt(entity.slice(2), 16) : parseInt(entity.slice(1), 10);
        return Number.isFinite(code) && code <= 0x10ffff ? String.fromCodePoint(code) : '';
      }
    }
  });
}

/** Los escapes de Excel: "_x000D_" es un retorno de carro y "_x005F_" un guion bajo. */
function unescapeExcel(text: string): string {
  return text.replace(/_x([0-9A-Fa-f]{4})_/g, (_, hex: string) => String.fromCharCode(parseInt(hex, 16)));
}

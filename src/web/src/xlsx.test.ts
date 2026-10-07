import { describe, expect, it } from 'vitest';
import { readHeaderLine } from './mapping';
import { formatNumber, isWorkbook, readWorkbookHeaderLine, toCsvLine } from './xlsx';

const MAIN = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
const RELS = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
const PACKAGE = 'http://schemas.openxmlformats.org/package/2006/relationships';

/** Un ZIP mínimo: cada parte comprimida como Excel (deflate) o guardada tal cual. */
async function zip(parts: Record<string, string>, stored = false): Promise<Blob> {
  const encoder = new TextEncoder();
  const locals: Uint8Array[] = [];
  const directory: Uint8Array[] = [];
  let offset = 0;

  for (const [name, content] of Object.entries(parts)) {
    const nameBytes = encoder.encode(name);
    const raw = encoder.encode(content);
    const data = stored ? raw : await deflate(raw);
    const method = stored ? 0 : 8;

    const local = new DataView(new ArrayBuffer(30));
    local.setUint32(0, 0x04034b50, true);
    local.setUint16(8, method, true);
    local.setUint32(18, data.length, true);
    local.setUint32(22, raw.length, true);
    local.setUint16(26, nameBytes.length, true);

    const entry = new DataView(new ArrayBuffer(46));
    entry.setUint32(0, 0x02014b50, true);
    entry.setUint16(10, method, true);
    entry.setUint32(20, data.length, true);
    entry.setUint32(24, raw.length, true);
    entry.setUint16(28, nameBytes.length, true);
    entry.setUint32(42, offset, true);

    locals.push(new Uint8Array(local.buffer), nameBytes, data);
    directory.push(new Uint8Array(entry.buffer), nameBytes);
    offset += 30 + nameBytes.length + data.length;
  }

  const directorySize = directory.reduce((sum, part) => sum + part.length, 0);
  const end = new DataView(new ArrayBuffer(22));
  end.setUint32(0, 0x06054b50, true);
  end.setUint16(8, Object.keys(parts).length, true);
  end.setUint16(10, Object.keys(parts).length, true);
  end.setUint32(12, directorySize, true);
  end.setUint32(16, offset, true);

  return new Blob([...locals, ...directory, new Uint8Array(end.buffer)].map((part) => part.slice()));
}

async function deflate(raw: Uint8Array): Promise<Uint8Array> {
  const stream = new Blob([raw.slice()]).stream().pipeThrough(new CompressionStream('deflate-raw'));
  return new Uint8Array(await new Response(stream).arrayBuffer());
}

function workbook(
  sheetXml: string,
  { shared = [] as string[], sheets = '<sheet name="Sheet1" sheetId="1" r:id="rId1"/>', stored = false } = {},
): Promise<Blob> {
  return zip(
    {
      '_rels/.rels': `<Relationships xmlns="${PACKAGE}"><Relationship Id="rId1" Type="${RELS}/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
      'xl/workbook.xml': `<workbook xmlns="${MAIN}" xmlns:r="${RELS}"><sheets>${sheets}</sheets></workbook>`,
      'xl/_rels/workbook.xml.rels':
        `<Relationships xmlns="${PACKAGE}">` +
        `<Relationship Id="rId1" Type="${RELS}/worksheet" Target="worksheets/sheet1.xml"/>` +
        `<Relationship Id="rId2" Type="${RELS}/worksheet" Target="worksheets/sheet2.xml"/>` +
        `<Relationship Id="rId9" Type="${RELS}/sharedStrings" Target="sharedStrings.xml"/></Relationships>`,
      'xl/worksheets/sheet1.xml': sheetXml,
      'xl/worksheets/sheet2.xml': `<worksheet xmlns="${MAIN}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Second</t></is></c></row></sheetData></worksheet>`,
      'xl/sharedStrings.xml': `<sst xmlns="${MAIN}">${shared.map((text) => `<si><t>${text}</t></si>`).join('')}</sst>`,
    },
    stored,
  );
}

const sheet = (rows: string) => `<worksheet xmlns="${MAIN}"><sheetData>${rows}</sheetData></worksheet>`;

describe('Excel workbook header', () => {
  it('reads the header row of shared strings, without any data row', async () => {
    const book = await workbook(
      sheet(
        '<row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c></row>' +
          '<row r="2"><c r="A2"><v>14381</v></c><c r="B2" t="s"><v>3</v></c></row>',
      ),
      { shared: ['EAI', 'Country', 'Application Name', 'Chile'] },
    );

    await expect(isWorkbook(book)).resolves.toBe(true);
    await expect(readWorkbookHeaderLine(book)).resolves.toBe('EAI,Country,Application Name');
  });

  it('is what the mapping review sends for a workbook, whatever its name', async () => {
    const book = await workbook(sheet('<row r="1"><c r="A1" t="inlineStr"><is><t>EAI</t></is></c></row>'));
    await expect(readHeaderLine(book)).resolves.toBe('EAI');
  });

  it('skips empty rows, keeps gaps and quotes what needs quotes', async () => {
    const book = await workbook(
      sheet(
        '<row r="1"/><row r="2"><c r="A2" s="3"/></row>' +
          '<row r="3"><c r="A3" t="inlineStr"><is><t>EAI</t></is></c>' +
          '<c r="C3" t="inlineStr"><is><r><t>Name, </t></r><r><t>&quot;short&quot; &amp; long</t></r><rPh><t>x</t></rPh></is></c></row>',
      ),
    );

    await expect(readWorkbookHeaderLine(book)).resolves.toBe('EAI,,"Name, ""short"" & long"');
  });

  it('reads the first visible sheet and prefixed elements', async () => {
    const book = await workbook(`<x:worksheet xmlns:x="${MAIN}"><x:sheetData><x:row r="1"><x:c r="A1" t="inlineStr"><x:is><x:t>Hidden</x:t></x:is></x:c></x:row></x:sheetData></x:worksheet>`, {
      sheets: '<sheet name="Lookups" sheetId="1" state="hidden" r:id="rId1"/><sheet name="APIM" sheetId="2" r:id="rId2"/>',
    });

    await expect(readWorkbookHeaderLine(book)).resolves.toBe('Second');

    const prefixed = await workbook(`<x:worksheet xmlns:x="${MAIN}"><x:sheetData><x:row r="1"><x:c r="A1" t="inlineStr"><x:is><x:t>EAI</x:t></x:is></x:c><x:c r="B1" t="inlineStr"><x:is><x:t>Country</x:t></x:is></x:c></x:row></x:sheetData></x:worksheet>`);
    await expect(readWorkbookHeaderLine(prefixed)).resolves.toBe('EAI,Country');
  });

  it('reads entries stored without compression', async () => {
    const book = await workbook(sheet('<row r="1"><c r="A1" t="inlineStr"><is><t>EAI</t></is></c><c r="B1"><v>2026</v></c></row>'), {
      stored: true,
    });

    await expect(readWorkbookHeaderLine(book)).resolves.toBe('EAI,2026');
  });

  it('stops decompressing once it has the header', async () => {
    // Detrás del encabezado, más de lo que se permite leer: solo pasa si no se lee.
    const book = await workbook(
      `<worksheet xmlns="${MAIN}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>EAI</t></is></c></row>` +
        '<row r="2">'.padEnd(12 * 1024 * 1024, 'x'),
    );

    await expect(readWorkbookHeaderLine(book)).resolves.toBe('EAI');
  });

  it('refuses what is not a usable workbook', async () => {
    await expect(isWorkbook(new Blob(['EAI,Country\n']))).resolves.toBe(false);
    await expect(readWorkbookHeaderLine(new Blob(['PK\u0003\u0004 not really a zip']))).rejects.toThrow();
    await expect(readWorkbookHeaderLine(await workbook(sheet('')))).rejects.toThrow();
  });
});

describe('numbers and CSV lines like the System layer writes them', () => {
  it.each([
    ['37', '37'],
    ['0.1', '0.1'],
    ['73.489999999999995', '73.49'],
    ['1.234', '1.2340'],
    ['1E-3', '0.0010'],
    ['1.5E+20', '150000000000000000000'],
  ])('%s is written %s', (raw, expected) => {
    expect(formatNumber(raw)).toBe(expected);
  });

  it('quotes commas, quotes, line breaks and edge spaces', () => {
    expect(toCsvLine(['EAI', 'a,b', 'say "hi"', ' padded', 'line\nbreak'])).toBe(
      'EAI,"a,b","say ""hi"""," padded","line\nbreak"',
    );
  });
});

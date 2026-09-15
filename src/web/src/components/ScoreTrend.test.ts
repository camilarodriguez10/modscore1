import { describe, expect, it } from 'vitest';
import { markerSlots, type TrendMarker } from './ScoreTrend';

const marker = (id: string, at: string): TrendMarker => ({ id, at, label: id, detail: '' });

const uploads = ['2026-08-01T00:00:00Z', '2026-08-11T00:00:00Z', '2026-08-31T00:00:00Z'];

describe('markerSlots', () => {
  it('places a marker between the uploads around it, in proportion to time', () => {
    const slots = markerSlots(uploads, [marker('v1.4', '2026-08-06T00:00:00Z'), marker('v1.5', '2026-08-21T00:00:00Z')]);

    expect(slots.map((s) => [s.marker.id, s.slot])).toEqual([
      ['v1.4', 0.5],
      ['v1.5', 1.5],
    ]);
  });

  it('keeps markers on an upload and drops those outside the series', () => {
    const slots = markerSlots(uploads, [
      marker('before', '2026-07-01T00:00:00Z'),
      marker('first', '2026-08-01T00:00:00Z'),
      marker('last', '2026-08-31T00:00:00Z'),
      marker('after', '2026-09-10T00:00:00Z'),
      marker('invalid', 'not a date'),
    ]);

    expect(slots.map((s) => [s.marker.id, s.slot])).toEqual([
      ['first', 0],
      ['last', 2],
    ]);
  });

  it('needs at least two uploads', () => {
    expect(markerSlots(['2026-08-01T00:00:00Z'], [marker('v1.4', '2026-08-01T00:00:00Z')])).toEqual([]);
  });
});

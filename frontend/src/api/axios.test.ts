import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { getAllPages } from './axios';
import { server } from '../test/server';

describe('getAllPages', () => {
  it('retrieves all API pages in order', async () => {
    server.use(http.get('http://localhost:5228/api/Expenses', ({ request }) => {
      const url = new URL(request.url);
      const page = Number(url.searchParams.get('page'));
      const pageSize = Number(url.searchParams.get('pageSize'));
      const items = page === 1
        ? Array.from({ length: 100 }, (_, index) => index + 1)
        : [101];

      return HttpResponse.json({ items, page, pageSize, totalCount: 101 });
    }));

    const items = await getAllPages<number>('/Expenses');

    expect(items).toHaveLength(101);
    expect(items[0]).toBe(1);
    expect(items[100]).toBe(101);
  });
});

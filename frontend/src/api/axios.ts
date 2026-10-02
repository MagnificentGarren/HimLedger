import axios from 'axios';
import { acquireEntraAccessToken } from '../auth/entra';

const api = axios.create({
  baseURL: 'http://localhost:5228/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use(async (config) => {
  if (config.headers.Authorization) return config;
  if (localStorage.getItem('authMethod') === 'entra') {
    const accessToken = await acquireEntraAccessToken();
    config.headers.Authorization = `Bearer ${accessToken}`;
    return config;
  }

  const token = localStorage.getItem('token');

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

export default api;

interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export async function getAllPages<T>(
  path: string,
  params: Record<string, string | number> = {},
): Promise<T[]> {
  const pageSize = 100;
  const items: T[] = [];
  let page = 1;
  let totalCount = Number.POSITIVE_INFINITY;

  while (items.length < totalCount) {
    const { data } = await api.get<PagedResponse<T>>(path, {
      params: { ...params, page, pageSize },
    });
    if (!Array.isArray(data.items)
      || data.page !== page
      || data.pageSize !== pageSize
      || !Number.isSafeInteger(data.totalCount)
      || data.totalCount < 0) {
      throw new Error(`The API returned an invalid paginated response for ${path}.`);
    }

    totalCount = data.totalCount;
    if (data.items.length === 0 && items.length < totalCount) {
      throw new Error(`The API returned an incomplete paginated response for ${path}.`);
    }

    items.push(...data.items);
    page += 1;
  }

  return items;
}

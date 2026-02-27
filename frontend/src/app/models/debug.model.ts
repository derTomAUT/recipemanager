import { PagedResult } from './recipe.model';

export interface AiDebugLogEntry {
  id: string;
  createdAtUtc: string;
  provider: string;
  model: string;
  operation: string;
  requestJsonSanitized: string;
  responseJsonSanitized: string;
  statusCode?: number;
  success: boolean;
  error?: string;
}

export interface AiDebugQuery {
  provider?: string;
  operation?: string;
  success?: boolean;
  page?: number;
  pageSize?: number;
}

export type AiDebugPagedResult = PagedResult<AiDebugLogEntry>;

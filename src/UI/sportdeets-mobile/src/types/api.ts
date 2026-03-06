export interface ApiResponse<T> {
  data: T;
  links?: Record<string, string>;
  ref?: string;
}

export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
  links?: Record<string, string>;
}

export interface ApiError {
  status: number;
  title: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Unwrap helper: pull `data` out of an ApiResponse */
export type Unwrap<T> = T extends ApiResponse<infer U>
  ? U
  : T extends PaginatedResponse<infer U>
  ? U[]
  : never;

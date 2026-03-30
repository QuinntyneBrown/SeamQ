import { Observable } from 'rxjs';

/**
 * Interface for tile data providers.
 */
export interface ITileDataProvider<T> {
  fetchData(query: TileQuery): Observable<T>;
}

export interface TileQuery {
  startTime: Date;
  endTime: Date;
  filters: Record<string, string>;
}

import { TileSize } from './tile-size.enum';

/**
 * Configuration for a tile instance.
 */
export interface ITileConfig {
  id: string;
  title: string;
  size: TileSize;
  refreshInterval: number | null;
}

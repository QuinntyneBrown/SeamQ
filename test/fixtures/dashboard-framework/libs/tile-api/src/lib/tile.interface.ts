/**
 * Interface that all tile plugins must implement.
 */
export interface ITileComponent {
  /** Called when the tile is initialized */
  onTileInit(): void;
  /** Called when the tile is destroyed */
  onTileDestroy(): void;
  /** Called when the tile is resized */
  onTileResize(size: TileSize): void;
}

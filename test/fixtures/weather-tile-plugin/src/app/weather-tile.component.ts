import { Component } from '@angular/core';
import { BaseTileComponent, ITileComponent, ITileConfig, TileSize, TILE_CONFIG_TOKEN } from '@dashboard/tile-api';

@Component({
  selector: 'weather-tile',
  standalone: true,
  template: '<div>Weather Tile</div>'
})
export class WeatherTileComponent extends BaseTileComponent implements ITileComponent {
  onTileInit(): void {
    console.log('Weather tile initialized');
  }

  onTileDestroy(): void {
    console.log('Weather tile destroyed');
  }

  onTileResize(size: TileSize): void {
    console.log('Weather tile resized to', size);
  }
}

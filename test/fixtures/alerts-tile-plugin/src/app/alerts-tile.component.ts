import { Component } from '@angular/core';
import { BaseTileComponent, ITileComponent, TILE_CONFIG_TOKEN } from '@dashboard/tile-api';

@Component({
  selector: 'alerts-tile',
  standalone: true,
  template: '<div>Alerts Tile</div>'
})
export class AlertsTileComponent extends BaseTileComponent implements ITileComponent {
  onTileInit(): void {
    console.log('Alerts tile initialized');
  }

  // INTENTIONAL VIOLATION: Missing onTileDestroy() - REQUIRED by contract
  // onTileDestroy(): void { }
}

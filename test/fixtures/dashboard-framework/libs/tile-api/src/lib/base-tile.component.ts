import { Component, Input, Output, EventEmitter } from '@angular/core';
import { ITileComponent } from './tile.interface';
import { ITileConfig } from './tile-config.interface';
import { TileSize } from './tile-size.enum';

@Component({ template: '' })
export abstract class BaseTileComponent implements ITileComponent {
  @Input() config!: ITileConfig;
  @Output() dataReady = new EventEmitter<TileDataReadyEvent>();

  abstract onTileInit(): void;
  abstract onTileDestroy(): void;
  onTileResize(size: TileSize): void {}
}

export interface TileDataReadyEvent {
  tileId: string;
  data: unknown;
}

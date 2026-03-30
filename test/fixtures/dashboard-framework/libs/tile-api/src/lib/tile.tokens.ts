import { InjectionToken } from '@angular/core';
import { ITileConfig } from './tile-config.interface';

export const TILE_CONFIG_TOKEN = new InjectionToken<ITileConfig>('TILE_CONFIG');
export const TILE_DATA_TOKEN = new InjectionToken<any>('TILE_DATA');

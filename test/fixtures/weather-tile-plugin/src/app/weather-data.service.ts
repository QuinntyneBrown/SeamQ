import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { ITileDataProvider, TileQuery } from '@dashboard/tile-api';

@Injectable({ providedIn: 'root' })
export class WeatherDataService implements ITileDataProvider<WeatherData> {
  fetchData(query: TileQuery): Observable<WeatherData> {
    return of({ temperature: 72, humidity: 45, windSpeed: 12 });
  }
}

export interface WeatherData {
  temperature: number;
  humidity: number;
  windSpeed: number;
}

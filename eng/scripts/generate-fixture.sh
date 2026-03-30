#!/usr/bin/env bash
# generate-fixture.sh — Creates Angular workspace fixtures for iteration $1
# Usage: ./generate-fixture.sh <iteration> <base_dir>
set -euo pipefail

ITER=${1:?Usage: generate-fixture.sh <iteration> <base_dir>}
BASE=${2:?Usage: generate-fixture.sh <iteration> <base_dir>}

# Complexity scaling: more types/services/components per iteration
NUM_INTERFACES=$((2 + ITER / 5))
NUM_SERVICES=$((1 + ITER / 10))
NUM_COMPONENTS=$((1 + ITER / 10))
NUM_INPUTS=$((1 + ITER / 15))
NUM_SUBJECTS=$((1 + ITER / 20))
NUM_TOKENS=$((1 + ITER / 20))
NUM_ENUMS=$((1 + ITER / 25))
NUM_ROUTES=$((ITER / 30))
NUM_STORE_ACTIONS=$((ITER / 25))
USE_SIGNALS=$( (( ITER >= 10 )) && echo 1 || echo 0 )
USE_STANDALONE=$( (( ITER >= 5 )) && echo 1 || echo 0 )
USE_NX=$( (( ITER >= 15 )) && echo 1 || echo 0 )
USE_SIGNALR=$( (( ITER >= 30 )) && echo 1 || echo 0 )
USE_HTTP_API=$( (( ITER >= 20 )) && echo 1 || echo 0 )
USE_MESSAGE_BUS=$( (( ITER >= 25 )) && echo 1 || echo 0 )

mkdir -p "$BASE"

########################################################################
# HELPER: generate a TypeScript interface with N fields
########################################################################
gen_interface() {
  local name="$1" num_fields="$2" doc="${3:-}"
  local out=""
  if [[ -n "$doc" ]]; then
    out+="/** ${doc} */\n"
  fi
  out+="export interface ${name} {\n"
  for i in $(seq 1 "$num_fields"); do
    local types=("string" "number" "boolean" "Date" "string[]" "Record<string, unknown>")
    local t=${types[$(( (RANDOM + i) % ${#types[@]} ))]}
    local optional=""
    (( RANDOM % 3 == 0 )) && optional="?"
    out+="  /** Field ${i} description */\n"
    out+="  field${i}${optional}: ${t};\n"
  done
  out+="}\n"
  echo -e "$out"
}

########################################################################
# HELPER: generate an enum
########################################################################
gen_enum() {
  local name="$1" num_members="$2"
  local out="export enum ${name} {\n"
  local members=("Active" "Inactive" "Pending" "Error" "Loading" "Ready" "Stale" "Disconnected" "Connected" "Unknown")
  for i in $(seq 1 "$num_members"); do
    local idx=$(( (i - 1) % ${#members[@]} ))
    out+="  ${members[$idx]}${i} = '${members[$idx],,}${i}',\n"
  done
  out+="}\n"
  echo -e "$out"
}

########################################################################
# HELPER: generate an Injectable service
########################################################################
gen_service() {
  local name="$1" num_methods="$2" deps="${3:-}"
  local out="import { Injectable } from '@angular/core';\n"
  if [[ -n "$deps" ]]; then
    out+="import { ${deps} } from './models';\n"
  fi
  out+="\n@Injectable({ providedIn: 'root' })\n"
  out+="export class ${name} {\n"
  for i in $(seq 1 "$num_methods"); do
    local ret_types=("void" "Observable<string>" "Promise<boolean>" "string" "number")
    local rt=${ret_types[$(( RANDOM % ${#ret_types[@]} ))]}
    out+="  /** Method ${i} documentation */\n"
    out+="  method${i}(param${i}: string): ${rt} {\n"
    out+="    throw new Error('not implemented');\n"
    out+="  }\n\n"
  done
  out+="}\n"
  echo -e "$out"
}

########################################################################
# HELPER: generate a Component
########################################################################
gen_component() {
  local name="$1" num_inputs="$2" num_outputs="$3" use_signals="$4"
  local standalone_flag=""
  [[ "$USE_STANDALONE" == "1" ]] && standalone_flag="  standalone: true,"
  local out="import { Component, Input, Output, EventEmitter } from '@angular/core';\n"
  if [[ "$use_signals" == "1" ]]; then
    out="import { Component, Input, Output, EventEmitter, input, model } from '@angular/core';\n"
  fi
  out+="\n@Component({\n  selector: 'app-${name,,}',\n${standalone_flag}\n  template: '<div></div>'\n})\n"
  out+="export class ${name}Component {\n"
  for i in $(seq 1 "$num_inputs"); do
    if [[ "$use_signals" == "1" ]] && (( i % 2 == 0 )); then
      out+="  /** Signal input ${i} */\n"
      out+="  readonly input${i} = input.required<string>();\n"
    else
      out+="  /** Input ${i} description */\n"
      out+="  @Input() input${i}: string = '';\n"
    fi
  done
  for i in $(seq 1 "$num_outputs"); do
    out+="  @Output() output${i} = new EventEmitter<string>();\n"
  done
  out+="}\n"
  echo -e "$out"
}

########################################################################
# HELPER: generate an InjectionToken
########################################################################
gen_token() {
  local name="$1" type="$2"
  echo -e "import { InjectionToken } from '@angular/core';\n\n/** Injection token for ${name} */\nexport const ${name} = new InjectionToken<${type}>('${name}');\n"
}

########################################################################
# HELPER: generate a barrel export (public-api.ts)
########################################################################
gen_barrel() {
  local files=("$@")
  local out=""
  for f in "${files[@]}"; do
    out+="export * from './${f}';\n"
  done
  echo -e "$out"
}

########################################################################
# WORKSPACE 1: lib-workspace-a (2 libraries: models + services)
########################################################################
WS1="$BASE/lib-workspace-a"
mkdir -p "$WS1/projects/shared-models/src/lib"
mkdir -p "$WS1/projects/shared-services/src/lib"

# angular.json
cat > "$WS1/angular.json" <<'AJSON'
{
  "version": 1,
  "$schema": "./node_modules/@angular/cli/lib/config/schema.json",
  "projects": {
    "shared-models": {
      "projectType": "library",
      "sourceRoot": "projects/shared-models/src"
    },
    "shared-services": {
      "projectType": "library",
      "sourceRoot": "projects/shared-services/src"
    }
  }
}
AJSON

# tsconfig.json
cat > "$WS1/tsconfig.json" <<TSCONF
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@shared/models": ["projects/shared-models/src/public-api"],
      "@shared/models/*": ["projects/shared-models/src/*"],
      "@shared/services": ["projects/shared-services/src/public-api"],
      "@shared/services/*": ["projects/shared-services/src/*"]
    },
    "target": "ES2022",
    "module": "ES2022",
    "strict": true
  }
}
TSCONF

cat > "$WS1/package.json" <<PJSON
{ "name": "lib-workspace-a", "version": "1.0.0" }
PJSON

# --- shared-models library ---
MODEL_FILES=()
for i in $(seq 1 "$NUM_INTERFACES"); do
  fname="model${i}"
  gen_interface "Model${i}" $(( 3 + ITER / 10 )) "Data transfer object for Model${i}" > "$WS1/projects/shared-models/src/lib/${fname}.ts"
  MODEL_FILES+=("lib/${fname}")
done
for i in $(seq 1 "$NUM_ENUMS"); do
  fname="enum${i}"
  gen_enum "Status${i}" $(( 3 + i )) > "$WS1/projects/shared-models/src/lib/${fname}.ts"
  MODEL_FILES+=("lib/${fname}")
done
# Token files
for i in $(seq 1 "$NUM_TOKENS"); do
  fname="token${i}"
  gen_token "TOKEN_${i}" "Model1" > "$WS1/projects/shared-models/src/lib/${fname}.ts"
  MODEL_FILES+=("lib/${fname}")
done
gen_barrel "${MODEL_FILES[@]}" > "$WS1/projects/shared-models/src/public-api.ts"

# --- shared-services library ---
SVC_FILES=()
for i in $(seq 1 "$NUM_SERVICES"); do
  fname="service${i}"
  gen_service "SharedService${i}" $(( 2 + ITER / 15 )) "Model1" > "$WS1/projects/shared-services/src/lib/${fname}.ts"
  SVC_FILES+=("lib/${fname}")
done
# Add RxJS subjects if iteration is high enough
if [[ "$USE_MESSAGE_BUS" == "1" ]]; then
  for i in $(seq 1 "$NUM_SUBJECTS"); do
    fname="bus${i}"
    cat > "$WS1/projects/shared-services/src/lib/${fname}.ts" <<BUS
import { Subject, BehaviorSubject, ReplaySubject } from 'rxjs';
import { Injectable } from '@angular/core';

/** Event bus ${i} for cross-workspace communication */
@Injectable({ providedIn: 'root' })
export class EventBus${i} {
  readonly events$ = new Subject<string>();
  readonly state$ = new BehaviorSubject<string>('initial');
  readonly history$ = new ReplaySubject<string>(10);

  emit(event: string): void { this.events$.next(event); }
}
BUS
    SVC_FILES+=("lib/${fname}")
  done
fi
gen_barrel "${SVC_FILES[@]}" > "$WS1/projects/shared-services/src/public-api.ts"


########################################################################
# WORKSPACE 2: lib-workspace-b (2 libraries: ui-components + state)
########################################################################
WS2="$BASE/lib-workspace-b"
mkdir -p "$WS2/projects/ui-components/src/lib"
mkdir -p "$WS2/projects/state-management/src/lib"

cat > "$WS2/angular.json" <<'AJSON'
{
  "version": 1,
  "projects": {
    "ui-components": {
      "projectType": "library",
      "sourceRoot": "projects/ui-components/src"
    },
    "state-management": {
      "projectType": "library",
      "sourceRoot": "projects/state-management/src"
    }
  }
}
AJSON

cat > "$WS2/tsconfig.json" <<TSCONF
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@ui/components": ["projects/ui-components/src/public-api"],
      "@ui/components/*": ["projects/ui-components/src/*"],
      "@state/management": ["projects/state-management/src/public-api"],
      "@state/management/*": ["projects/state-management/src/*"]
    },
    "target": "ES2022",
    "module": "ES2022",
    "strict": true
  }
}
TSCONF

cat > "$WS2/package.json" <<PJSON
{ "name": "lib-workspace-b", "version": "1.0.0" }
PJSON

# --- ui-components library ---
UI_FILES=()
for i in $(seq 1 "$NUM_COMPONENTS"); do
  fname="component${i}"
  gen_component "Widget${i}" "$NUM_INPUTS" $(( 1 + ITER / 20 )) "$USE_SIGNALS" > "$WS2/projects/ui-components/src/lib/${fname}.ts"
  UI_FILES+=("lib/${fname}")
done
gen_barrel "${UI_FILES[@]}" > "$WS2/projects/ui-components/src/public-api.ts"

# --- state-management library ---
STATE_FILES=()
if (( NUM_STORE_ACTIONS > 0 )); then
  cat > "$WS2/projects/state-management/src/lib/actions.ts" <<ACTS
import { createAction, props } from '@ngrx/store';

$(for i in $(seq 1 "$NUM_STORE_ACTIONS"); do
echo "export const loadItems${i} = createAction('[Items] Load ${i}');"
echo "export const loadItems${i}Success = createAction('[Items] Load ${i} Success', props<{ items: string[] }>());"
done)
ACTS
  STATE_FILES+=("lib/actions")

  cat > "$WS2/projects/state-management/src/lib/selectors.ts" <<SELS
import { createFeatureSelector, createSelector } from '@ngrx/store';

export interface AppState { items: string[]; loading: boolean; }
export const selectAppState = createFeatureSelector<AppState>('app');
export const selectItems = createSelector(selectAppState, (state) => state.items);
export const selectLoading = createSelector(selectAppState, (state) => state.loading);
SELS
  STATE_FILES+=("lib/selectors")
fi

# Add a store interface
cat > "$WS2/projects/state-management/src/lib/store.ts" <<STORE
import { Injectable } from '@angular/core';

export interface StoreState {
  items: string[];
  loading: boolean;
  error: string | null;
}

@Injectable({ providedIn: 'root' })
export class AppStore {
  private state: StoreState = { items: [], loading: false, error: null };

  getItems(): string[] { return this.state.items; }
  isLoading(): boolean { return this.state.loading; }
}
STORE
STATE_FILES+=("lib/store")

gen_barrel "${STATE_FILES[@]}" > "$WS2/projects/state-management/src/public-api.ts"


########################################################################
# WORKSPACE 3: app-workspace (1 application consuming libs)
########################################################################
WS3="$BASE/app-workspace"
mkdir -p "$WS3/src/app/services"
mkdir -p "$WS3/src/app/components"

cat > "$WS3/angular.json" <<'AJSON'
{
  "version": 1,
  "projects": {
    "main-app": {
      "projectType": "application",
      "sourceRoot": "src"
    }
  }
}
AJSON

# tsconfig with paths to simulate npm link
cat > "$WS3/tsconfig.json" <<TSCONF
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@shared/models": ["../lib-workspace-a/projects/shared-models/src/public-api"],
      "@shared/services": ["../lib-workspace-a/projects/shared-services/src/public-api"],
      "@ui/components": ["../lib-workspace-b/projects/ui-components/src/public-api"],
      "@state/management": ["../lib-workspace-b/projects/state-management/src/public-api"]
    },
    "target": "ES2022",
    "module": "ES2022",
    "strict": true
  }
}
TSCONF

cat > "$WS3/package.json" <<PJSON
{ "name": "app-workspace", "version": "1.0.0" }
PJSON

# App service that consumes shared models/services
cat > "$WS3/src/app/services/app.service.ts" <<SVC
import { Injectable } from '@angular/core';
import { Model1 } from '@shared/models';
import { SharedService1 } from '@shared/services';

@Injectable({ providedIn: 'root' })
export class AppService {
  constructor(private shared: SharedService1) {}

  getData(): Model1 {
    throw new Error('not implemented');
  }
}
SVC

# App component consuming ui-components
cat > "$WS3/src/app/components/dashboard.component.ts" <<COMP
import { Component } from '@angular/core';
import { Widget1Component } from '@ui/components';
import { AppStore } from '@state/management';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: '<app-widget1 [input1]="title"></app-widget1>'
})
export class DashboardComponent {
  title = 'Dashboard';
  constructor(private store: AppStore) {}
}
COMP

# Route config for higher iterations
if (( NUM_ROUTES > 0 )); then
  cat > "$WS3/src/app/app.routes.ts" <<ROUTES
import { Routes } from '@angular/router';

export const routes: Routes = [
$(for i in $(seq 1 "$NUM_ROUTES"); do
echo "  { path: 'feature${i}', loadComponent: () => import('./components/dashboard.component').then(m => m.DashboardComponent) },"
done)
];
ROUTES
fi

# Barrel for app
cat > "$WS3/src/public-api.ts" <<BARREL
export * from './app/services/app.service';
export * from './app/components/dashboard.component';
BARREL

# HTTP API patterns for higher iterations
if [[ "$USE_HTTP_API" == "1" ]]; then
  cat > "$WS3/src/app/services/api.service.ts" <<API
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Model1 } from '@shared/models';

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: string;
}

export interface RequestDto {
  id: string;
  payload: Record<string, unknown>;
}

export interface ResponseDto {
  id: string;
  result: string;
  timestamp: Date;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  getModels(): Observable<ApiResponse<Model1[]>> {
    return this.http.get<ApiResponse<Model1[]>>('/api/models');
  }

  sendRequest(dto: RequestDto): Observable<ResponseDto> {
    return this.http.post<ResponseDto>('/api/requests', dto);
  }
}
API
fi

# SignalR hub patterns for higher iterations
if [[ "$USE_SIGNALR" == "1" ]]; then
  cat > "$WS3/src/app/services/telemetry.service.ts" <<HUB
import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';

export enum ConnectionState {
  Connected = 'connected',
  Connecting = 'connecting',
  Disconnected = 'disconnected'
}

export interface TelemetryRecord {
  id: string;
  value: number;
  timestamp: Date;
  quality: 'good' | 'bad' | 'stale';
}

@Injectable({ providedIn: 'root' })
export class TelemetryService {
  readonly connectionState$ = new BehaviorSubject<ConnectionState>(ConnectionState.Disconnected);
  readonly telemetry$ = new Subject<TelemetryRecord>();

  connect(hubUrl: string): void { /* SignalR connection */ }
  subscribe(channels: string[]): void { /* Subscribe to channels */ }
  disconnect(): void { /* Cleanup */ }
}
HUB
fi


########################################################################
# WORKSPACE 4: standalone-app (ng new style)
########################################################################
WS4="$BASE/standalone-app"
mkdir -p "$WS4/src/app"

cat > "$WS4/angular.json" <<'AJSON'
{
  "version": 1,
  "projects": {
    "standalone-app": {
      "projectType": "application",
      "sourceRoot": "src"
    }
  }
}
AJSON

cat > "$WS4/tsconfig.json" <<TSCONF
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@shared/models": ["../lib-workspace-a/projects/shared-models/src/public-api"],
      "@shared/services": ["../lib-workspace-a/projects/shared-services/src/public-api"],
      "@ui/components": ["../lib-workspace-b/projects/ui-components/src/public-api"],
      "@state/management": ["../lib-workspace-b/projects/state-management/src/public-api"]
    },
    "target": "ES2022",
    "module": "ES2022",
    "strict": true
  }
}
TSCONF

cat > "$WS4/package.json" <<PJSON
{ "name": "standalone-app", "version": "1.0.0" }
PJSON

cat > "$WS4/src/app/app.component.ts" <<COMP
import { Component, inject } from '@angular/core';
import { Model1 } from '@shared/models';
import { SharedService1 } from '@shared/services';
import { Widget1Component } from '@ui/components';
import { AppStore } from '@state/management';

@Component({
  selector: 'app-root',
  standalone: true,
  template: '<h1>Standalone App</h1>'
})
export class AppComponent {
  private svc = inject(SharedService1);
  private store = inject(AppStore);
}
COMP

cat > "$WS4/src/public-api.ts" <<BARREL
export * from './app/app.component';
BARREL

echo "FIXTURES_CREATED iter=${ITER} ws1=${WS1} ws2=${WS2} ws3=${WS3} ws4=${WS4}"

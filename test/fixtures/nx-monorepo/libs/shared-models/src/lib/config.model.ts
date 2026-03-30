export interface ConfigModel {
  apiUrl: string;
  featureFlags: Record<string, boolean>;
  theme: 'light' | 'dark';
}

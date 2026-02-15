// Matches Hermes\Storage\Repositories\TeamConfiguration\TeamConfigurationDocument.cs
export interface TeamConfiguration {
  teamId: string;
  teamName: string;
  iterationPath: string;
  areaPaths: string[];
  slaOverrides: Record<string, number>;
  createdAt: string;
  updatedAt?: string;
}

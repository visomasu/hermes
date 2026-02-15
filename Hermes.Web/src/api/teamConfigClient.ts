import type { TeamConfiguration } from '../types/teamConfig';

const API_BASE_URL = 'http://localhost:3978/api';

export async function listTeamConfigs(): Promise<TeamConfiguration[]> {
  const response = await fetch(`${API_BASE_URL}/teamconfiguration`);

  if (!response.ok) {
    throw new Error(`Failed to list team configurations: ${response.statusText}`);
  }

  return response.json();
}

export async function getTeamConfig(teamId: string): Promise<TeamConfiguration | null> {
  const response = await fetch(`${API_BASE_URL}/teamconfiguration/${encodeURIComponent(teamId)}`);

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch team configuration: ${response.statusText}`);
  }

  return response.json();
}

export async function createTeamConfig(config: TeamConfiguration): Promise<TeamConfiguration> {
  const response = await fetch(`${API_BASE_URL}/teamconfiguration`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(config),
  });

  if (!response.ok) {
    throw new Error(`Failed to create team configuration: ${response.statusText}`);
  }

  return response.json();
}

export async function updateTeamConfig(
  teamId: string,
  config: TeamConfiguration
): Promise<TeamConfiguration> {
  const response = await fetch(
    `${API_BASE_URL}/teamconfiguration/${encodeURIComponent(teamId)}`,
    {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(config),
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to update team configuration: ${response.statusText}`);
  }

  return response.json();
}

export async function deleteTeamConfig(teamId: string): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/teamconfiguration/${encodeURIComponent(teamId)}`,
    {
      method: 'DELETE',
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to delete team configuration: ${response.statusText}`);
  }
}

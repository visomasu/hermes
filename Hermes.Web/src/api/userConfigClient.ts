import type { UserConfiguration } from '../types/userConfig';

const API_BASE_URL = 'http://localhost:3978/api';

export async function getUserConfig(userId: string): Promise<UserConfiguration | null> {
  const response = await fetch(`${API_BASE_URL}/user-config/${encodeURIComponent(userId)}`);

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch user configuration: ${response.statusText}`);
  }

  return response.json();
}

export async function updateUserConfig(
  userId: string,
  config: Partial<UserConfiguration>
): Promise<UserConfiguration> {
  const response = await fetch(
    `${API_BASE_URL}/user-config/${encodeURIComponent(userId)}`,
    {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(config),
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to update user configuration: ${response.statusText}`);
  }

  return response.json();
}

export async function deleteUserConfig(userId: string): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/user-config/${encodeURIComponent(userId)}`,
    {
      method: 'DELETE',
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to delete user configuration: ${response.statusText}`);
  }
}

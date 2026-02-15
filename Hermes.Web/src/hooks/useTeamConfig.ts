import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getTeamConfig, updateTeamConfig, createTeamConfig, deleteTeamConfig, listTeamConfigs } from '../api/teamConfigClient';
import type { TeamConfiguration } from '../types/teamConfig';

export function useTeamConfigs() {
  return useQuery({
    queryKey: ['teamConfigs'],
    queryFn: listTeamConfigs,
  });
}

export function useTeamConfig(teamId: string) {
  return useQuery({
    queryKey: ['teamConfig', teamId],
    queryFn: () => getTeamConfig(teamId),
    enabled: !!teamId,
  });
}

export function useCreateTeamConfig() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (config: TeamConfiguration) => createTeamConfig(config),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teamConfigs'] });
    },
  });
}

export function useUpdateTeamConfig() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ teamId, config }: { teamId: string; config: TeamConfiguration }) =>
      updateTeamConfig(teamId, config),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['teamConfig', data.teamId] });
      queryClient.invalidateQueries({ queryKey: ['teamConfigs'] });
    },
  });
}

export function useDeleteTeamConfig() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (teamId: string) => deleteTeamConfig(teamId),
    onSuccess: (_, teamId) => {
      queryClient.invalidateQueries({ queryKey: ['teamConfig', teamId] });
      queryClient.invalidateQueries({ queryKey: ['teamConfigs'] });
    },
  });
}

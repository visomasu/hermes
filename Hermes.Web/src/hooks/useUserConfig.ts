import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getUserConfig, updateUserConfig, deleteUserConfig } from '../api/userConfigClient';
import type { UserConfiguration } from '../types/userConfig';

export function useUserConfig(userId: string) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ['userConfig', userId],
    queryFn: () => getUserConfig(userId),
    enabled: !!userId,
  });

  const updateMutation = useMutation({
    mutationFn: (config: Partial<UserConfiguration>) => updateUserConfig(userId, config),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userConfig', userId] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteUserConfig(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userConfig', userId] });
    },
  });

  return {
    config: query.data,
    isLoading: query.isLoading,
    error: query.error,
    updateConfig: updateMutation.mutate,
    deleteConfig: deleteMutation.mutate,
    isUpdating: updateMutation.isPending,
    isDeleting: deleteMutation.isPending,
  };
}

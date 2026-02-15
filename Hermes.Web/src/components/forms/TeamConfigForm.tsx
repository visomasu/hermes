import { useState, useEffect } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import Card from '../shared/Card';
import Input from '../shared/Input';
import Button from '../shared/Button';
import { useTeamConfig, useUpdateTeamConfig, useTeamConfigs, useCreateTeamConfig, useDeleteTeamConfig } from '../../hooks/useTeamConfig';
import type { TeamConfiguration } from '../../types/teamConfig';

interface SlaOverride {
  workItemType: string;
  days: number;
}

interface TeamConfigFormData {
  teamId: string;
  teamName: string;
  iterationPath: string;
  areaPaths: Array<{ value: string }>;
  slaOverrides: Array<SlaOverride>;
}

export default function TeamConfigForm() {
  const [selectedTeamId, setSelectedTeamId] = useState<string>('');
  const [isCreating, setIsCreating] = useState(false);

  const { data: teams, isLoading: teamsLoading } = useTeamConfigs();
  const { data: teamConfig, isLoading: configLoading } = useTeamConfig(selectedTeamId);
  const updateTeamConfig = useUpdateTeamConfig();
  const createTeamConfig = useCreateTeamConfig();
  const deleteTeamConfig = useDeleteTeamConfig();

  const { register, handleSubmit, reset, control, formState: { errors, isDirty } } = useForm<TeamConfigFormData>({
    defaultValues: {
      teamId: '',
      teamName: '',
      iterationPath: '',
      areaPaths: [{ value: '' }],
      slaOverrides: [],
    }
  });

  const { fields: areaPathFields, append: appendAreaPath, remove: removeAreaPath } = useFieldArray({
    control,
    name: 'areaPaths',
  });

  const { fields: slaOverrideFields, append: appendSlaOverride, remove: removeSlaOverride } = useFieldArray({
    control,
    name: 'slaOverrides',
  });

  // Load form data when team config is fetched
  useEffect(() => {
    if (teamConfig && !isCreating) {
      const slaOverrides = Object.entries(teamConfig.slaOverrides || {}).map(([workItemType, days]) => ({
        workItemType,
        days,
      }));

      const areaPaths = teamConfig.areaPaths.length > 0
        ? teamConfig.areaPaths.map(path => ({ value: path }))
        : [{ value: '' }];

      reset({
        teamId: teamConfig.teamId,
        teamName: teamConfig.teamName,
        iterationPath: teamConfig.iterationPath,
        areaPaths,
        slaOverrides,
      });
    }
  }, [teamConfig, reset, isCreating]);

  const onSubmit = async (data: TeamConfigFormData) => {
    const slaOverrides: Record<string, number> = {};
    data.slaOverrides.forEach((override) => {
      if (override.workItemType && override.days > 0) {
        slaOverrides[override.workItemType] = override.days;
      }
    });

    const config: TeamConfiguration = {
      teamId: data.teamId,
      teamName: data.teamName,
      iterationPath: data.iterationPath,
      areaPaths: data.areaPaths.map(ap => ap.value).filter((path) => path.trim() !== ''),
      slaOverrides,
      createdAt: teamConfig?.createdAt || new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    try {
      if (isCreating) {
        await createTeamConfig.mutateAsync(config);
        setIsCreating(false);
        setSelectedTeamId(config.teamId);
      } else {
        await updateTeamConfig.mutateAsync({ teamId: data.teamId, config });
      }
    } catch (error) {
      console.error('Failed to save team configuration:', error);
    }
  };

  const handleDelete = async () => {
    if (!selectedTeamId) return;
    if (!confirm(`Are you sure you want to delete team "${teamConfig?.teamName}"?`)) return;

    try {
      await deleteTeamConfig.mutateAsync(selectedTeamId);
      setSelectedTeamId('');
    } catch (error) {
      console.error('Failed to delete team configuration:', error);
    }
  };

  const handleCreateNew = () => {
    setIsCreating(true);
    setSelectedTeamId('');
    reset({
      teamId: '',
      teamName: '',
      iterationPath: '',
      areaPaths: [{ value: '' }],
      slaOverrides: [],
    });
  };

  const handleCancelCreate = () => {
    setIsCreating(false);
    setSelectedTeamId('');
  };

  if (teamsLoading) {
    return (
      <Card title="Team Configuration">
        <div className="text-center py-12">
          <div className="animate-spin w-12 h-12 border-4 border-purple-500 border-t-transparent rounded-full mx-auto mb-4"></div>
          <p className="text-gray-600">Loading teams...</p>
        </div>
      </Card>
    );
  }

  return (
    <Card title="Team Configuration">
      <div className="space-y-6">
        {/* Team Selection / Create New */}
        {!isCreating && (
          <div className="bg-white rounded-xl p-6 border-2 border-gray-100">
            <div className="flex items-end gap-4">
              <div className="flex-1">
                <label className="block text-sm font-semibold text-gray-700 mb-2">
                  Select Team
                </label>
                <div className="relative">
                  <select
                    value={selectedTeamId}
                    onChange={(e) => setSelectedTeamId(e.target.value)}
                    className="w-full px-4 py-3 bg-white border-2 border-gray-200 rounded-xl shadow-sm focus:outline-none focus:ring-4 focus:ring-purple-100 focus:border-purple-500 transition-all appearance-none cursor-pointer hover:border-gray-300"
                  >
                    <option value="">-- Select a team --</option>
                    {teams?.map((team) => (
                      <option key={team.teamId} value={team.teamId}>
                        {team.teamName}
                      </option>
                    ))}
                  </select>
                  <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-4 text-gray-500">
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                    </svg>
                  </div>
                </div>
              </div>
              <Button
                type="button"
                onClick={handleCreateNew}
                className="bg-gradient-to-br from-green-500 to-green-600 hover:from-green-600 hover:to-green-700 whitespace-nowrap"
              >
                + Create New Team
              </Button>
            </div>
          </div>
        )}

        {/* Form */}
        {(selectedTeamId || isCreating) && (
          <>
            {configLoading ? (
              <div className="text-center py-12">
                <div className="animate-spin w-12 h-12 border-4 border-purple-500 border-t-transparent rounded-full mx-auto mb-4"></div>
                <p className="text-gray-600">Loading team configuration...</p>
              </div>
            ) : (
              <form onSubmit={handleSubmit(onSubmit)} className="space-y-8">
                {/* Basic Info */}
                <div>
                  <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center gap-2">
                    <span className="text-2xl">ℹ️</span>
                    Basic Information
                  </h3>
                  <div className="space-y-4 bg-white rounded-xl p-6 border-2 border-gray-100">
                    <Input
                      label="Team ID"
                      placeholder="e.g., contact-center-ai"
                      {...register('teamId', { required: 'Team ID is required' })}
                      error={errors.teamId?.message}
                      disabled={!isCreating}
                    />
                    <Input
                      label="Team Name"
                      placeholder="e.g., Contact Center AI"
                      {...register('teamName', { required: 'Team name is required' })}
                      error={errors.teamName?.message}
                    />
                  </div>
                </div>

                {/* Iteration Path */}
                <div>
                  <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center gap-2">
                    <span className="text-2xl">📅</span>
                    Iteration Path
                  </h3>
                  <div className="space-y-4 bg-white rounded-xl p-6 border-2 border-gray-100">
                    <Input
                      label="Current Iteration"
                      placeholder="e.g., OneCRM\\FY26\\Q3\\1Wk\\1Wk33"
                      {...register('iterationPath', { required: 'Iteration path is required' })}
                      error={errors.iterationPath?.message}
                    />
                    <p className="text-sm text-gray-500">
                      Example: "OneCRM\FY26\Q3\1Wk\1Wk33" for weekly sprints
                    </p>
                  </div>
                </div>

                {/* Area Paths */}
                <div>
                  <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center gap-2">
                    <span className="text-2xl">📁</span>
                    Area Paths
                  </h3>
                  <div className="space-y-4 bg-white rounded-xl p-6 border-2 border-gray-100">
                    {areaPathFields.map((field, index) => (
                      <div key={field.id} className="flex gap-2">
                        <Input
                          label={index === 0 ? 'Area Paths' : ''}
                          placeholder="e.g., OneCRM\\AI\\ContactCenter"
                          {...register(`areaPaths.${index}.value` as const)}
                        />
                        {areaPathFields.length > 1 && (
                          <button
                            type="button"
                            onClick={() => removeAreaPath(index)}
                            className="mt-auto mb-1 px-3 py-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                          >
                            ✕
                          </button>
                        )}
                      </div>
                    ))}
                    <Button
                      type="button"
                      onClick={() => appendAreaPath({ value: '' })}
                      className="bg-blue-100 text-blue-700 hover:bg-blue-200 shadow-none"
                    >
                      + Add Area Path
                    </Button>
                  </div>
                </div>

                {/* SLA Overrides */}
                <div>
                  <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center gap-2">
                    <span className="text-2xl">⏰</span>
                    SLA Overrides
                  </h3>
                  <div className="space-y-4 bg-white rounded-xl p-6 border-2 border-gray-100">
                    <p className="text-sm text-gray-600 mb-4">
                      Define team-specific SLA thresholds by work item type. Leave empty to use global defaults.
                    </p>
                    {slaOverrideFields.map((field, index) => (
                      <div key={field.id} className="flex gap-4">
                        <div className="flex-1">
                          <Input
                            label={index === 0 ? 'Work Item Type' : ''}
                            placeholder="e.g., Task, Bug, User Story"
                            {...register(`slaOverrides.${index}.workItemType` as const)}
                          />
                        </div>
                        <div className="w-32">
                          <Input
                            label={index === 0 ? 'Days' : ''}
                            type="number"
                            placeholder="3"
                            {...register(`slaOverrides.${index}.days` as const, {
                              valueAsNumber: true,
                            })}
                          />
                        </div>
                        <button
                          type="button"
                          onClick={() => removeSlaOverride(index)}
                          className="mt-auto mb-1 px-3 py-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                    <Button
                      type="button"
                      onClick={() => appendSlaOverride({ workItemType: '', days: 0 })}
                      className="bg-blue-100 text-blue-700 hover:bg-blue-200 shadow-none"
                    >
                      + Add SLA Override
                    </Button>
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center justify-between pt-6 border-t-2 border-gray-200">
                  <div className="flex gap-2">
                    {!isCreating && (
                      <Button
                        type="button"
                        onClick={handleDelete}
                        disabled={deleteTeamConfig.isPending}
                        className="bg-red-500 hover:bg-red-600"
                      >
                        {deleteTeamConfig.isPending ? 'Deleting...' : 'Delete Team'}
                      </Button>
                    )}
                    {isCreating && (
                      <Button
                        type="button"
                        onClick={handleCancelCreate}
                        className="bg-gray-300 text-gray-700 hover:bg-gray-400"
                      >
                        Cancel
                      </Button>
                    )}
                  </div>
                  <div className="flex items-center gap-4">
                    <div className="text-sm text-gray-500">
                      {isDirty ? '• Unsaved changes' : '✓ All changes saved'}
                    </div>
                    <Button
                      type="submit"
                      disabled={!isDirty || updateTeamConfig.isPending || createTeamConfig.isPending}
                    >
                      {isCreating
                        ? createTeamConfig.isPending
                          ? 'Creating...'
                          : 'Create Team'
                        : updateTeamConfig.isPending
                        ? 'Saving...'
                        : 'Save Changes'}
                    </Button>
                  </div>
                </div>

                {/* Success/Error Messages */}
                {(updateTeamConfig.isSuccess || createTeamConfig.isSuccess) && (
                  <div className="bg-green-50 border border-green-200 rounded-xl p-4">
                    <p className="text-sm text-green-800 flex items-center gap-2">
                      <span className="text-lg">✓</span>
                      {isCreating ? 'Team created successfully!' : 'Team configuration updated successfully!'}
                    </p>
                  </div>
                )}

                {(updateTeamConfig.isError || createTeamConfig.isError || deleteTeamConfig.isError) && (
                  <div className="bg-red-50 border border-red-200 rounded-xl p-4">
                    <p className="text-sm text-red-800 flex items-center gap-2">
                      <span className="text-lg">⚠</span>
                      Failed to {isCreating ? 'create' : deleteTeamConfig.isError ? 'delete' : 'update'} team configuration. Please try again.
                    </p>
                  </div>
                )}
              </form>
            )}
          </>
        )}

        {/* Empty State */}
        {!selectedTeamId && !isCreating && teams && teams.length === 0 && (
          <div className="text-center py-12">
            <div className="w-20 h-20 bg-gradient-to-br from-purple-100 to-purple-200 rounded-2xl flex items-center justify-center mx-auto mb-6">
              <span className="text-5xl">👥</span>
            </div>
            <h3 className="text-xl font-semibold text-gray-900 mb-3">
              No Teams Configured
            </h3>
            <p className="text-gray-600 max-w-md mx-auto mb-6">
              Get started by creating your first team configuration.
            </p>
            <Button onClick={handleCreateNew} className="bg-gradient-to-br from-green-500 to-green-600 hover:from-green-600 hover:to-green-700">
              + Create Your First Team
            </Button>
          </div>
        )}
      </div>
    </Card>
  );
}

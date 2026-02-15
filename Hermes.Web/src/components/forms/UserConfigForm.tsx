import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useUserConfig } from '../../hooks/useUserConfig';
import Card from '../shared/Card';
import Input from '../shared/Input';
import Toggle from '../shared/Toggle';
import Button from '../shared/Button';

const TEST_USER_ID = 'testuser@microsoft.com'; // Hardcoded for MVP

const userConfigSchema = z.object({
  slaViolationNotifications: z.boolean(),
  workItemUpdateNotifications: z.boolean(),
  maxNotificationsPerHour: z.number().min(1).max(100),
  maxNotificationsPerDay: z.number().min(1).max(1000),
  timeZoneId: z.string().min(1),
  quietHoursEnabled: z.boolean(),
  quietHoursStartTime: z.string().regex(/^\d{2}:\d{2}$/),
  quietHoursEndTime: z.string().regex(/^\d{2}:\d{2}$/),
});

type UserConfigFormData = z.infer<typeof userConfigSchema>;

export default function UserConfigForm() {
  const { config, isLoading, updateConfig, isUpdating } = useUserConfig(TEST_USER_ID);

  const {
    register,
    handleSubmit,
    watch,
    reset,
    formState: { errors, isDirty },
  } = useForm<UserConfigFormData>({
    resolver: zodResolver(userConfigSchema),
    defaultValues: {
      slaViolationNotifications: true,
      workItemUpdateNotifications: true,
      maxNotificationsPerHour: 10,
      maxNotificationsPerDay: 50,
      timeZoneId: 'Pacific Standard Time',
      quietHoursEnabled: false,
      quietHoursStartTime: '22:00',
      quietHoursEndTime: '08:00',
    },
  });

  // Watch toggle values for visual state
  const slaViolationNotifications = watch('slaViolationNotifications');
  const workItemUpdateNotifications = watch('workItemUpdateNotifications');
  const quietHoursEnabled = watch('quietHoursEnabled');

  useEffect(() => {
    if (config) {
      reset({
        slaViolationNotifications: config.notifications.slaViolationNotifications,
        workItemUpdateNotifications: config.notifications.workItemUpdateNotifications,
        maxNotificationsPerHour: config.notifications.maxNotificationsPerHour,
        maxNotificationsPerDay: config.notifications.maxNotificationsPerDay,
        timeZoneId: config.notifications.timeZoneId,
        quietHoursEnabled: config.notifications.quietHours?.enabled ?? false,
        quietHoursStartTime: config.notifications.quietHours?.startTime ?? '22:00',
        quietHoursEndTime: config.notifications.quietHours?.endTime ?? '08:00',
      });
    }
  }, [config, reset]);

  const onSubmit = (data: UserConfigFormData) => {
    updateConfig({
      teamsUserId: TEST_USER_ID,
      notifications: {
        slaViolationNotifications: data.slaViolationNotifications,
        workItemUpdateNotifications: data.workItemUpdateNotifications,
        maxNotificationsPerHour: data.maxNotificationsPerHour,
        maxNotificationsPerDay: data.maxNotificationsPerDay,
        timeZoneId: data.timeZoneId,
        quietHours: data.quietHoursEnabled
          ? {
              enabled: true,
              startTime: data.quietHoursStartTime,
              endTime: data.quietHoursEndTime,
            }
          : undefined,
      },
      createdAt: config?.createdAt ?? new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    });
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading configuration...</p>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {/* Notification Preferences */}
      <Card title="Notification Preferences">
        <div className="space-y-4">
          <Toggle
            {...register('slaViolationNotifications')}
            checked={slaViolationNotifications}
            label="SLA Violation Notifications"
          />
          <Toggle
            {...register('workItemUpdateNotifications')}
            checked={workItemUpdateNotifications}
            label="Work Item Update Notifications"
          />

          <div className="grid grid-cols-2 gap-4 pt-4">
            <Input
              {...register('maxNotificationsPerHour', { valueAsNumber: true })}
              type="number"
              label="Max Notifications Per Hour"
              error={errors.maxNotificationsPerHour?.message}
            />
            <Input
              {...register('maxNotificationsPerDay', { valueAsNumber: true })}
              type="number"
              label="Max Notifications Per Day"
              error={errors.maxNotificationsPerDay?.message}
            />
          </div>

          <Input
            {...register('timeZoneId')}
            type="text"
            label="Time Zone"
            placeholder="Pacific Standard Time"
            error={errors.timeZoneId?.message}
          />
        </div>
      </Card>

      {/* Quiet Hours */}
      <Card title="Quiet Hours">
        <div className="space-y-4">
          <Toggle
            {...register('quietHoursEnabled')}
            checked={quietHoursEnabled}
            label="Enable Quiet Hours"
          />

          {quietHoursEnabled && (
            <div className="grid grid-cols-2 gap-4 pt-4">
              <Input
                {...register('quietHoursStartTime')}
                type="time"
                label="Start Time"
                error={errors.quietHoursStartTime?.message}
              />
              <Input
                {...register('quietHoursEndTime')}
                type="time"
                label="End Time"
                error={errors.quietHoursEndTime?.message}
              />
            </div>
          )}
        </div>
      </Card>

      {/* SLA Registration */}
      {config?.slaRegistration && (
        <Card title="SLA Registration">
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-gray-700">Status:</span>
              <span
                className={`px-2 py-1 text-xs rounded-full ${
                  config.slaRegistration.isRegistered
                    ? 'bg-green-100 text-green-800'
                    : 'bg-gray-100 text-gray-800'
                }`}
              >
                {config.slaRegistration.isRegistered ? 'Registered' : 'Not Registered'}
              </span>
            </div>
            <div className="text-sm text-gray-600">
              <p>Email: {config.slaRegistration.azureDevOpsEmail}</p>
              <p>Manager: {config.slaRegistration.isManager ? 'Yes' : 'No'}</p>
              {config.slaRegistration.isManager && (
                <p>Direct Reports: {config.slaRegistration.directReportEmails.length}</p>
              )}
            </div>
          </div>
        </Card>
      )}

      {/* Save Button */}
      <div className="flex justify-end gap-4">
        <Button
          type="button"
          variant="secondary"
          onClick={() => reset()}
          disabled={!isDirty || isUpdating}
        >
          Reset
        </Button>
        <Button type="submit" disabled={!isDirty || isUpdating}>
          {isUpdating ? 'Saving...' : 'Save Changes'}
        </Button>
      </div>
    </form>
  );
}

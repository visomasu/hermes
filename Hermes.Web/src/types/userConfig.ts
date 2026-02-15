export interface UserConfiguration {
  teamsUserId: string;
  notifications: NotificationPreferences;
  slaRegistration?: SlaRegistrationProfile;
  createdAt: string;
  updatedAt: string;
}

export interface NotificationPreferences {
  slaViolationNotifications: boolean;
  workItemUpdateNotifications: boolean;
  maxNotificationsPerHour: number;
  maxNotificationsPerDay: number;
  timeZoneId: string;
  quietHours?: QuietHours;
}

export interface QuietHours {
  enabled: boolean;
  startTime: string; // HH:mm format
  endTime: string;
}

export interface SlaRegistrationProfile {
  isRegistered: boolean;
  azureDevOpsEmail: string;
  directReportEmails: string[];
  isManager: boolean;
  subscribedTeamIds: string[];
  registeredAt: string;
}

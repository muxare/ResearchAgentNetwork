export type TaskItem = {
  id: string;
  description: string;
  status: string;
  createdAt?: string;
  priority?: number;
  parentTaskId?: string;
  isSystemTask?: boolean;
  category?: string;
  metadata?: Record<string, any>;
};


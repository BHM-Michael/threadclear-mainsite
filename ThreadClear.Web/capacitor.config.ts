import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.bhmit.threadclear',
  appName: 'ThreadClear',
  webDir: 'dist/thread-clear.web',
  server: {
    androidScheme: 'https'
  }
};

export default config;

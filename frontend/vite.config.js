import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    allowedHosts: [
      'mathinsight.me',
      'www.mathinsight.me',
      'mathinsight-frontend.livelydune-15e29de0.eastasia.azurecontainerapps.io',
    ],
  },
  preview: {
    port: 4173,
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.js',
  },
});


import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiTarget = process.env.PMGM_API_PROXY_TARGET ?? 'http://localhost:5000'
const basePath = process.env.VITE_BASE_PATH ?? '/'

export default defineConfig({
  base: basePath,
  plugins: [react()],
  // Una sola hoja de estilos: los módulos cargados bajo demanda no pueden alterar la cascada institucional.
  build: { cssCodeSplit: false },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/health': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
    plugins: [react()],

    server: {
        proxy: {
            '/api': {
                target: 'https://localhost:7259',
                changeOrigin: true,
                secure: false,
            },
        },
    },

    build: {
        outDir: '../wwwroot/react-food-search',
        emptyOutDir: true,

        rollupOptions: {
            output: {
                entryFileNames: 'assets/food-search.js',
                chunkFileNames: 'assets/[name].js',
                assetFileNames: (assetInfo) => {
                    if (assetInfo.name?.endsWith('.css')) {
                        return 'assets/food-search.css'
                    }

                    return 'assets/[name][extname]'
                },
            },
        },
    },
})
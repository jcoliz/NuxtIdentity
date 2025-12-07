// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  vite: {
    server: {
      warmup: {
        // Pre-warm these files for faster initial load
        clientFiles: ['**/*.vue'],
      }
    },
    css: {
      preprocessorOptions: {
        scss: {
          quietDeps: true,
          // You can also silence specific deprecation types from your own code if needed
          silenceDeprecations: ['import', 'color-functions'],
        },
      },
    },
  },
  // Reduce build time in development
  sourcemap: {
    server: false,  // Disable server sourcemaps for faster startup
    client: true    // Keep client sourcemaps for debugging
  },
  css: [
    '~/assets/scss/custom.scss'
  ],
  router: {
    options: {
      linkActiveClass: 'active'
    }
  },
  modules: ['@sidebase/nuxt-auth'],
  auth: {
    originEnvKey: 'NUXT_PUBLIC_API_BASE_URL',
    provider: {
      type: 'local',
      endpoints: {
        signIn: { path: '/api/auth/login', method: 'post' }, // ADD THIS LINE
        signOut: { path: '/api/auth/logout', method: 'post' },
        getSession: { path: '/api/auth/user' },
        signUp: { path: '/api/auth/signup', method: 'post' }
      },
      // not 'pages'??
      pages: {
        login: '/login', // Path to the login page (where unauthenticated users are sent)
      },
      token: {
        signInResponseTokenPointer: '/token/accessToken'
      },
      refresh: {
        isEnabled: true,
        endpoint: { path: '/api/auth/refresh', method: 'post' },
        refreshOnlyToken: false, //??
        token: {
          signInResponseRefreshTokenPointer: '/token/refreshToken',
          refreshResponseTokenPointer: '/token/accessToken',
          refreshRequestTokenPointer: '/refreshToken'
        },
      },
      session: {
        dataType:  {
          id: 'string',
          email: 'string',
          name: 'string',
          roles: 'string[]', // Updated to match your UserInfo model
          claims: '{ type:string, value:string }[]'
        },
        dataResponsePointer: '/user'
      }
    },
    sessionRefresh: {
      // Whether to refresh the session every time the browser window is refocused.
      enableOnWindowFocus: true, // for testing
      // Whether to refresh the session every `X` milliseconds. Set this to `false` to turn it off. The session will only be refreshed if a session already exists.
      enablePeriodically: 60000, // for testing, was 5000
      // Custom refresh handler - uncomment to use
      // handler: './config/AuthRefreshHandler'
    },
    globalAppMiddleware: {
      isEnabled: true
    }
  },
  runtimeConfig: {
    public: {
      // This value is overwritten during the static generation step
      // by the NUXT_PUBLIC_API_BASE_URL environment variable.
      // Don't fill this in with a default value here, or it will cause problems!
      apiBaseUrl: ``,
    }
  }
})

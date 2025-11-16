// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  vite: {
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
    baseURL: 'http://localhost:5074/api/auth', // Update this to your backend URL
    provider: {
      type: 'local',
      endpoints: {
        getSession: { path: '/user' },
        signUp: { path: '/signup', method: 'post' }
      },
      pages: {
        login: '/login',
      },
      token: {
        signInResponseTokenPointer: '/token/accessToken'
      },
      refresh: {
        isEnabled: true,
        endpoint: { path: '/refresh', method: 'post' },
        refreshOnlyToken: true, //??
        token: {
          signInResponseRefreshTokenPointer: '/token/refreshToken',
          refreshResponseTokenPointer: '/token/refreshToken',
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
      enableOnWindowFocus: true,
      // Whether to refresh the session every `X` milliseconds. Set this to `false` to turn it off. The session will only be refreshed if a session already exists.
      enablePeriodically: 5000, // just for demo!!
      // Custom refresh handler - uncomment to use
      // handler: './config/AuthRefreshHandler'
    },
    globalAppMiddleware: {
      isEnabled: true
    },
    runtimeConfig: {
      public : {
        authOrigin: 'http://localhost:3000/'
      }
    }
  }
})

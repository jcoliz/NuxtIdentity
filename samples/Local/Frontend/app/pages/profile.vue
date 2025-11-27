<script setup lang="ts">
import { jwtDecode } from 'jwt-decode'

useHead({
  title: 'Profile',
});

const { token, refreshToken, refresh, status } = useAuth()

// Computed properties to strip "Bearer " prefix from tokens
const cleanToken = computed(() => {
  if (!token.value) return null
  return token.value.startsWith('Bearer ') ? token.value.substring(7) : token.value
})

const cleanRefreshToken = computed(() => {
  if (!refreshToken.value) return null
  return refreshToken.value
})

// Decode the JWT token payload
const decodedToken = computed(() => {
  if (!cleanToken.value) return null
  try {
    return jwtDecode(cleanToken.value)
  } catch (error) {
    console.error('Failed to decode JWT:', error)
    return null
  }
})

// Decode the JWT header to get algorithm info
const decodedHeader = computed(() => {
  if (!cleanToken.value) return null
  try {
    return jwtDecode(cleanToken.value, { header: true })
  } catch (error) {
    console.error('Failed to decode JWT header:', error)
    return null
  }
})

// Format the token expiration
const formatExpiration = (exp: number | undefined) => {
  if (!exp) return 'N/A'
  return new Date(exp * 1000).toLocaleString()
}

// Format the token issued at
const formatIssuedAt = (iat: number | undefined) => {
  if (!iat) return 'N/A'
  return new Date(iat * 1000).toLocaleString()
}

// Check if token is expired
const isTokenExpired = computed(() => {
  if (!decodedToken.value?.exp) return false
  return Date.now() >= decodedToken.value.exp * 1000
})

// Helper function to get claim descriptions
type ClaimType = 'iss' | 'sub' | 'aud' | 'exp' | 'iat' | 'nbf' | 'jti' | 'name' | 'email' | 'role' | 'scope'

const getClaimDescription = (claim: string) => {
  const descriptions: Record<ClaimType, string> = {
    'iss': 'Issuer - who issued the token',
    'sub': 'Subject - who the token is about',
    'aud': 'Audience - who the token is intended for',
    'exp': 'Expiration time - when the token expires',
    'iat': 'Issued at - when the token was issued',
    'nbf': 'Not before - when the token becomes valid',
    'jti': 'JWT ID - unique identifier for the token',
    'name': 'Full name of the user',
    'email': 'Email address of the user',
    'role': 'User roles or permissions',
    'scope': 'Access scopes granted to the token'
  }
  return descriptions[claim as ClaimType] || ''
}

// Helper function to copy text to clipboard
const copyToClipboard = async (text: string) => {
  if (process.client && window.navigator?.clipboard) {
    try {
      await window.navigator.clipboard.writeText(text)
    } catch (err) {
      console.error('Failed to copy to clipboard:', err)
    }
  }
}

// Refresh token handler
const isRefreshing = ref(false)
const refreshError = ref<string | null>(null)

const handleRefresh = async () => {
  isRefreshing.value = true
  refreshError.value = null

  try {
    await refresh()
  } catch (error) {
    console.error('Failed to refresh token:', error)
    refreshError.value = error instanceof Error ? error.message : 'Failed to refresh token'
  } finally {
    isRefreshing.value = false
  }
}

</script>

<template>
  <div class="container mt-4">
    <h1 class="mb-3">Profile</h1>

    <div v-if="!cleanToken" class="alert alert-warning">
      <FeatherIcon icon="alert-triangle" size="16" class="me-2" />
      No JWT token found. Please log in.
    </div>

    <div v-else-if="!decodedToken" class="alert alert-danger">
      <FeatherIcon icon="x-circle" size="16" class="me-2" />
      Failed to decode JWT token. Token may be malformed.
    </div>

    <div v-else>
      <!-- Token Status -->
      <div class="card mb-4">
        <div class="card-header bg-info text-white">
          <h5 class="card-title mb-0 d-flex align-items-center">
            <FeatherIcon icon="key" size="20" class="me-2" />
            <span>Token Information</span>
          </h5>
        </div>
        <div class="card-body">
          <div class="row">
            <div class="col-md-6">
              <strong>Status:</strong>
              <span v-if="isTokenExpired" class="badge bg-danger ms-2 p-1">Expired</span>
              <span v-else class="badge bg-success ms-2 p-1">Valid</span>
            </div>
            <div class="col-md-6">
              <strong>Algorithm:</strong> {{ decodedHeader?.alg || 'N/A' }}
            </div>
            <div class="col-md-6">
              <strong>Issued At:</strong> {{ formatIssuedAt(decodedToken.iat) }}
            </div>
            <div class="col-md-6">
              <strong>Expires:</strong> {{ formatExpiration(decodedToken.exp) }}
            </div>
          </div>
        </div>
      </div>

      <!-- JWT Claims -->
      <div class="card mb-4">
        <div class="card-header bg-primary text-white">
          <h5 class="card-title mb-0 d-flex align-items-center">
            <FeatherIcon icon="list" size="20" class="me-2" />
            <span>JWT Claims</span>
          </h5>
        </div>
        <div class="card-body">
          <div class="table-responsive">
            <table class="table table-striped">
              <thead>
                <tr>
                  <th>Claim</th>
                  <th>Value</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="[key, value] in Object.entries(decodedToken)" :key="key">
                  <td>
                    <code>{{ key }}</code>
                    <small v-if="getClaimDescription(key)" class="text-muted d-block">
                      {{ getClaimDescription(key) }}
                    </small>
                  </td>
                  <td>
                    <span v-if="Array.isArray(value)">
                      <span v-for="item in value" :key="item" class="badge bg-secondary me-1">
                        {{ item }}
                      </span>
                    </span>
                    <span v-else-if="typeof value === 'object'">
                      <pre class="mb-0">{{ JSON.stringify(value, null, 2) }}</pre>
                    </span>
                    <span v-else-if="key === 'exp' || key === 'iat' || key === 'nbf'">
                      <code>{{ value }}</code>
                      <small class="text-muted d-block">
                        {{ formatExpiration(value) }}
                      </small>
                    </span>
                    <span v-else>
                      {{ value }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Refresh Token Section -->
      <div class="card mb-4">
        <div class="card-header bg-warning text-dark">
          <h5 class="card-title mb-0 d-flex align-items-center justify-content-between">
            <span class="d-flex align-items-center">
              <FeatherIcon icon="refresh-cw" size="20" class="me-2" />
              <span>Refresh Token</span>
            </span>
            <button
              class="btn btn-sm btn-dark"
              :disabled="isRefreshing || status === 'loading'"
              @click="handleRefresh"
            >
              <FeatherIcon
                icon="refresh-cw"
                size="14"
                :class="{ 'spin': isRefreshing }"
                class="me-1"
              />
              {{ isRefreshing ? 'Refreshing...' : 'Refresh Tokens' }}
            </button>
          </h5>
        </div>
        <div class="card-body">
          <div v-if="refreshError" class="alert alert-danger alert-dismissible fade show mb-3" role="alert">
            <FeatherIcon icon="alert-circle" size="16" class="me-2" />
            {{ refreshError }}
            <button type="button" class="btn-close" @click="refreshError = null"></button>
          </div>

          <div v-if="cleanRefreshToken" class="input-group">
            <textarea
              class="form-control font-monospace"
              :value="cleanRefreshToken"
              readonly
              rows="3"
              style="resize: none;"
            ></textarea>
            <button
              class="btn btn-outline-secondary"
              type="button"
              @click="copyToClipboard(cleanRefreshToken)"
              title="Copy refresh token"
            >
              <FeatherIcon icon="copy" size="16" />
            </button>
          </div>
          <div v-else class="text-muted">
            <FeatherIcon icon="info" size="16" class="me-2" />
            No refresh token available
          </div>
        </div>
      </div>

      <!-- Raw Token Display -->
      <div class="card mt-4">
        <div class="card-header bg-secondary text-white">
          <h6 class="card-title mb-0 d-flex align-items-center">
            <FeatherIcon icon="code" size="16" class="me-2" />
            <span>Raw JWT Token</span>
          </h6>
        </div>
        <div class="card-body">
          <div class="input-group">
            <textarea
              class="form-control font-monospace"
              :value="cleanToken"
              readonly
              rows="4"
              style="resize: none;"
            ></textarea>
            <button
              class="btn btn-outline-secondary"
              type="button"
              @click="copyToClipboard(cleanToken)"
              title="Copy token"
            >
              <FeatherIcon icon="copy" size="16" />
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.font-monospace {
  font-size: 0.875rem;
}

pre {
  font-size: 0.875rem;
  max-height: 200px;
  overflow-y: auto;
}

.table code {
  background-color: #f8f9fa;
  padding: 0.2rem 0.4rem;
  border-radius: 0.25rem;
  font-size: 0.875rem;
}

.card {
  box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.spin {
  animation: spin 1s linear infinite;
}
</style>

<script setup lang="ts">
const { token, refreshToken, data, status, lastRefreshedAt } = useAuth()

// Computed properties to strip "Bearer " prefix from tokens
const cleanToken = computed(() => {
  if (!token.value) return null
  return token.value.startsWith('Bearer ') ? token.value.substring(7) : token.value
})

// Helper function to truncate long tokens for display
const truncateToken = (token: string | null, length = 20) => {
  if (!token) return 'N/A'
  return token.length > length ? `${token.substring(0, length)}...` : token
}

// Helper function to format the last refreshed time
const formatRefreshTime = (timestamp: any) => {
  if (!timestamp) return 'No refresh happened'
  return new Date(timestamp).toLocaleString()
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

// Status badge variant based on auth status
const statusVariant = computed(() => {
  switch (status.value) {
    case 'authenticated': return 'success'
    case 'loading': return 'warning'
    case 'unauthenticated': return 'danger'
    default: return 'secondary'
  }
})
</script>

<template>
  <div class="card mt-4">
    <div class="card-header bg-primary text-white">
        <h5 class="card-title mb-0 d-flex align-items-center">
            <FeatherIcon icon="shield" size="20" class="me-2 icon-up-2" />
            <span>Authentication Status</span>
        </h5>
    </div>
    <div class="card-body">
      <!-- Status Badge -->
      <div class="mb-3">
        <span class="badge fs-6 me-2 p-1" :class="`bg-${statusVariant}`" data-testid="status">
          {{ status }}
        </span>
        <small class="text-muted">Current authentication status</small>
      </div>

      <!-- User Data -->
      <div class="mb-3">
        <h6 class="text-muted mb-2">
          <FeatherIcon icon="user" size="16" class="me-1" />
          Session Data
        </h6>
        <div v-if="data && data" class="bg-light p-3 rounded">
          <div class="row g-2">
            <div class="col-sm-6">
              <strong>Name:</strong> {{ data.name || 'N/A' }}
            </div>
            <div class="col-sm-6">
              <strong>Email:</strong> {{ data.email || 'N/A' }}
            </div>
            <div class="col-sm-6">
              <strong>ID:</strong> {{ data.id || 'N/A' }}
            </div>
            <div class="col-sm-6">
              <strong>Roles:</strong> 
              <span v-if="data.roles && data.roles.length">
                <span v-for="role in data.roles" :key="role" class="badge bg-info me-1 p-1">
                  {{ role }}
                </span>
              </span>
              <span v-else class="text-muted">None</span>
            </div>
          </div>
        </div>
        <div v-else class="alert alert-info mb-0">
          <FeatherIcon icon="info" size="16" class="me-1" />
          No session data present. Are you logged in?
        </div>
      </div>

      <!-- Tokens Section -->
      <div class="mb-3">
        <h6 class="text-muted mb-2">
          <FeatherIcon icon="key" size="16" class="me-1" />
          Tokens
        </h6>
        <div class="row g-2">
          <!-- Access Token -->
          <div class="col-12">
            <div class="input-group">
              <span class="input-group-text bg-success text-white">
                <FeatherIcon icon="lock" size="16" />
              </span>
              <input 
                type="text" 
                class="form-control font-monospace" 
                :value="truncateToken(cleanToken, 50)" 
                readonly
                :placeholder="token ? 'Access Token' : 'No access token present'"
              >
              <button 
                v-if="token" 
                class="btn btn-outline-secondary" 
                type="button"
                @click="copyToClipboard(cleanToken ?? '')"
                title="Copy token"
              >
                <FeatherIcon icon="copy" size="16" />
              </button>
            </div>
            <small class="text-muted">Access Token</small>
          </div>

          <!-- Refresh Token -->
          <div class="col-12">
            <div class="input-group">
              <span class="input-group-text bg-warning text-dark">
                <FeatherIcon icon="refresh-cw" size="16" />
              </span>
              <input 
                type="text" 
                class="form-control font-monospace" 
                :value="truncateToken(refreshToken, 50)" 
                readonly
                :placeholder="refreshToken ? 'Refresh Token' : 'No refresh token available'"
              >
              <button 
                v-if="refreshToken" 
                class="btn btn-outline-secondary" 
                type="button"
                @click="copyToClipboard(refreshToken)"
                title="Copy token"
              >
                <FeatherIcon icon="copy" size="16" />
              </button>
            </div>
            <small class="text-muted">Refresh Token</small>
          </div>
        </div>
      </div>

      <!-- Last Refresh Time -->
      <div class="d-flex align-items-center text-muted">
        <FeatherIcon icon="clock" size="16" class="me-2" />
        <small>
          <strong>Last refreshed:</strong> {{ formatRefreshTime(lastRefreshedAt) }}
        </small>
      </div>
    </div>
  </div>
</template>

<style scoped>
.font-monospace {
  font-size: 0.875rem;
}

.card {
  box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
}

.badge {
  text-transform: capitalize;
}

.input-group-text {
  min-width: 45px;
  justify-content: center;
}

.bg-light {
  background-color: #f8f9fa !important;
}
/* Artificially adjust shield icon to appear visually centered */
.icon-up-2 {
  transform: translateY(-2px);
}
</style>

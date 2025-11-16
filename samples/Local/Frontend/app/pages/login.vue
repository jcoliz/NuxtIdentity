<script setup lang="ts">
useHead({
  title: 'Login',
});

definePageMeta({
  auth: {
    unauthenticatedOnly: true,
    navigateAuthenticatedTo: '/'
  }
})

const { signIn, status } = useAuth()

const username = ref('')
const password = ref('')
const isLoading = ref(false)
const showPassword = ref(false)
const errorMessage = ref('')

const handleLogin = async () => {
  if (!username.value || !password.value) {
    errorMessage.value = 'Please enter both username and password'
    return
  }

  try {
    isLoading.value = true
    errorMessage.value = ''
    await signIn({ 
      username: username.value, 
      password: password.value 
    })
  } catch (error: any) {
    console.error('Login error:', error)
    errorMessage.value = error.message || 'Login failed. Please check your credentials.'
  } finally {
    isLoading.value = false
  }
}

const togglePasswordVisibility = () => {
  showPassword.value = !showPassword.value
}
</script>

<template>
  <div class="min-vh-100 d-flex align-items-center bg-light">
    <div class="container">
      <div class="row justify-content-center">
        <div class="col-md-6 col-lg-4">
          <!-- Login Card -->
          <div class="card shadow-lg border-0 rounded-lg">
            <div class="card-header bg-primary text-white text-center py-4">
              <div class="mb-2">
                🛡️
              </div>
              <h3 class="mb-0">Welcome Back</h3>
              <p class="mb-0 opacity-75">Sign in to your account</p>
            </div>
            
            <div class="card-body p-4">
              <!-- Status Badge -->
              <div v-if="status !== 'unauthenticated'" class="text-center mb-3">
                <span class="badge bg-info p-1">{{ status }}</span>
              </div>

              <!-- Error Message -->
              <div v-if="errorMessage" class="alert alert-danger d-flex align-items-center mb-3">
                ⚠️ {{ errorMessage }}
              </div>

              <!-- Login Form -->
              <form @submit.prevent="handleLogin">
                <!-- Username Field -->
                <div class="mb-3">
                  <label for="username" class="form-label">
                    👤 Username
                  </label>
                  <input 
                    id="username"
                    v-model="username" 
                    type="text" 
                    class="form-control form-control-lg"
                    placeholder="Enter your username"
                    :disabled="isLoading"
                    required
                    autocomplete="username"
                  >
                </div>

                <!-- Password Field -->
                <div class="mb-4">
                  <label for="password" class="form-label">
                    🔒 Password
                  </label>
                  <div class="input-group">
                    <input 
                      id="password"
                      v-model="password" 
                      :type="showPassword ? 'text' : 'password'" 
                      class="form-control form-control-lg"
                      placeholder="Enter your password"
                      :disabled="isLoading"
                      required
                      autocomplete="current-password"
                    >
                    <button 
                      type="button" 
                      class="btn btn-outline-secondary"
                      @click="togglePasswordVisibility"
                      :disabled="isLoading"
                      tabindex="-1"
                    >
                      {{ showPassword ? '🙈' : '👁️' }}
                    </button>
                  </div>
                </div>

                <!-- Submit Button -->
                <div class="d-grid mb-3">
                  <button 
                    type="submit" 
                    class="btn btn-primary btn-lg"
                    :disabled="isLoading || !username || !password"
                  >
                    <span v-if="isLoading" class="spinner-border spinner-border-sm me-2" role="status">
                      <span class="visually-hidden">Loading...</span>
                    </span>
                    {{ isLoading ? '🔄 Signing In...' : '🔐 Sign In' }}
                  </button>
                </div>
              </form>

              <!-- Additional Links -->
              <div class="text-center">
                <p class="text-muted mb-2">Don't have an account?</p>
                <NuxtLink to="/signup" class="btn btn-outline-primary btn-sm">
                  ➕ Create Account
                </NuxtLink>
              </div>
            </div>

            <!-- Footer -->
            <div class="card-footer bg-light text-center py-3">
              <small class="text-muted">
                🛡️ Secured with NuxtIdentity
              </small>
            </div>
          </div>

          <!-- Demo Credentials -->
          <div class="card mt-3 border-warning">
            <div class="card-body bg-warning bg-opacity-10 py-2">
              <small class="text-warning">
                ℹ️ <strong>Demo:</strong> Try any username/password to create an account
              </small>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.min-vh-100 {
  min-height: 100vh;
}

.card {
  transition: transform 0.2s ease-in-out;
}

.card:hover {
  transform: translateY(-2px);
}

.form-control:focus {
  border-color: #0d6efd;
  box-shadow: 0 0 0 0.2rem rgba(13, 110, 253, 0.25);
}

.btn-primary {
  background: linear-gradient(45deg, #0d6efd, #6f42c1);
  border: none;
}

.btn-primary:hover {
  background: linear-gradient(45deg, #0b5ed7, #59359a);
  transform: translateY(-1px);
}

.spinner-border-sm {
  width: 1rem;
  height: 1rem;
}

.bg-light {
  background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%) !important;
}

.shadow-lg {
  box-shadow: 0 1rem 3rem rgba(0, 0, 0, 0.175) !important;
}
</style>

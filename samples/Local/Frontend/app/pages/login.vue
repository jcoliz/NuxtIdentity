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
    },{ 
      redirect: true, 
      callbackUrl: '/' 
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
                <FeatherIcon icon="shield" size="48" class="icon-up-1" />
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
                <FeatherIcon icon="alert-triangle" size="16" class="me-2" />
                {{ errorMessage }}
              </div>

              <!-- Login Form -->
              <form @submit.prevent="handleLogin">
                <!-- Username Field -->
                <div class="mb-3">
                  <label for="username" class="form-label">
                    <FeatherIcon icon="user" size="24" /> Username
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
                    <FeatherIcon icon="lock" size="24" class="icon-up-2" /> Password
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
                      <FeatherIcon :icon="showPassword ? 'eye-off' : 'eye'" size="16" />
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
                    <span v-if="!isLoading">
                      <FeatherIcon icon="log-in" size="16" class="me-2" />
                      Sign In
                    </span>
                    <span v-else>
                      Signing In...
                    </span>
                  </button>
                </div>
              </form>

              <!-- Additional Links -->
              <div class="text-center">
                <p class="text-muted mb-2">Don't have an account?</p>
                <NuxtLink to="/register" class="btn btn-outline-primary btn-sm">
                  <FeatherIcon icon="user-plus" size="16" class="me-1 icon-up-2" /> Create Account
                </NuxtLink>
              </div>
            </div>

            <!-- Footer -->
            <div class="card-footer bg-light text-center py-3">
              <small class="text-muted">
                <FeatherIcon icon="shield" size="14" class="me-1" />
                Secured with NuxtIdentity
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

/* Artificially adjust shield icon to appear visually centered */
.icon-up-1 {
  transform: translateY(-1px);
}
/* Artificially adjust shield icon to appear visually centered */
.icon-up-2 {
  transform: translateY(-2px);
}
</style>

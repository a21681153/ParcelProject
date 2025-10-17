const loginObj = {
    data() {
        return {
            form: { username: '', password: '' },
            loading: false,
            errorMessage:''
        };
    },
    methods: {
        doLogin() {
            if (this.loading) return;

            if (!this.form.username || !this.form.password) {
                this.errorMessage = '請輸入帳號與密碼';
                return;
            }
            this.loading = true;
            this.errorMessage = '';

            const token = document.querySelector(
                'input[name="__RequestVerificationToken"]').value;

            const requestData = {
                Username: this.form.username,
                Password: this.form.password,
            };
            axios.post('/Account/Login', requestData, {
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                withCredentials: true                      // 夾帶 Cookie
            })
                .then(r => {
                    console.log('登入回應:', r.data);
                    if (r.data.ok) {
                        /* 記錄 JWT 供後續頁面使用 */
                        localStorage.setItem('jwt', r.data.token);
                        window.location.href = r.data.url;              // 依角色導頁
                    } else {
                        this.errorMessage = r.data.msg || '登入失敗';
                    }
                })
                .catch(error => {
                    console.error('登入錯誤:', error); 
                    console.error('錯誤回應', error.r?.data);
                    this.errorMessage = error.r?.data?.msg || '連線錯誤，請稍後再試';
                })
                .finally(() => {
                    this.loading = false;
                });
        },
        handleKeyDown(event) {
            if (event.key === 'Enter' && !this.loading) {
                event.preventDefault();
                this.doLogin();
            }
        }
    },
    mounted() {
        // 頁面載入完成後，自動聚焦到帳號輸入框
        this.$nextTick(() => {
            const usernameInput = document.getElementById('username');
            const passwordInput = document.getElementById('password');
            usernameInput?.focus();
            if (usernameInput) {
                usernameInput.addEventListener('keydown', this.handleKeyDown);
            }
            if (passwordInput) {
                passwordInput.addEventListener('keydown', this.handleKeyDown);
            }

            const form = document.getElementById('loginForm');
            if (form) {
                form.addEventListener('submit', (e) => {
                    e.preventDefault();
                    this.doLogin();
                });
            }
        });
    },
    beforeUnmount() {
        const usernameInput = document.getElementById('username');
        const passwordInput = document.getElementById('password');
        const form = document.getElementById('loginForm');

        if (usernameInput) {
            usernameInput.removeEventListener('keydown', this.handleKeyDown);
        }
        if (passwordInput) {
            passwordInput.removeEventListener('keydown', this.handleKeyDown);
        }
        if (form) {
            form.removeEventListener('submit', this.handleSubmit);
        }
    }
};

/* 只有登入頁存在 #LoginApp 時才掛載 */
document.addEventListener('DOMContentLoaded', function () {
    const host = document.getElementById('LoginApp');
    if (host && typeof Vue !== 'undefined') {
        Vue.createApp(loginObj).mount('#LoginApp');
        console.log('Vue.js 應用程式已掛載');
    } else {
        console.error('Vue.js 載入失敗或找不到 LoginApp 元素');
    }
});

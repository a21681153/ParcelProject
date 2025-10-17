// 請在 _Layout.cshtml 或每個頁面 <script> 前載入此檔
axios.interceptors.request.use(cfg => {
    const tk = localStorage.getItem('jwt');
    if (tk) cfg.headers.Authorization = 'Bearer ' + tk;
    return cfg;
});
axios.interceptors.response.use(
    r => r,
    err => {
        // 權杖過期 → 401
        if (err.response && err.response.status === 401) {
            localStorage.removeItem('jwt');
            alert('登入逾時，請重新登入');
            window.location.href = '/Account/Login';
        }
        return Promise.reject(err);
    });
// 請在 _Layout.cshtml 或每個頁面 <script> 前載入此檔
axios.interceptors.request.use(cfg => {
    const tk = localStorage.getItem('jwt');
    if (tk) cfg.headers.Authorization = 'Bearer ' + tk;
    return cfg;
});
axios.interceptors.response.use(
    r => r,
    err => {
        // 權杖過期 → 401
        if (err.response && err.response.status === 401) {
            localStorage.removeItem('jwt');
            alert('登入逾時，請重新登入');
            window.location.href = '/Account/Login';
        }
        return Promise.reject(err);
    });

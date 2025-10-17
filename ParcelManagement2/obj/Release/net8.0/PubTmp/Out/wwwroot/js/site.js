// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// 登出功能
function logout() {
    // 取得 CSRF token
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    
    if (!token) {
        console.error('找不到 CSRF token');
        return;
    }

    fetch('/Account/Logout', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token
        },
        body: `__RequestVerificationToken=${encodeURIComponent(token)}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.ok) {
            alert(data.msg || '登出成功');
            if (data.redirectUrl) {
                window.location.href = data.redirectUrl;
            } else {
                window.location.href = '/Account/Login';
            }
        } else {
            alert(data.msg || '登出失敗');
        }
    })
    .catch(error => {
        console.error('登出錯誤:', error);
        alert('登出時發生錯誤，請稍後再試');
    });
}

// 簡單的登出連結處理 (適用於 GET 請求)
function logoutRedirect() {
    window.location.href = '/Account/LogoutGet';
}

// 綁定登出按鈕事件 (當頁面載入時自動執行)
document.addEventListener('DOMContentLoaded', function() {
    // 查找所有 class 為 'logout-btn' 的按鈕並綁定點擊事件
    const logoutButtons = document.querySelectorAll('.logout-btn');
    logoutButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.preventDefault();
            logout();
        });
    });

    // 查找所有 class 為 'logout-link' 的連結並綁定點擊事件
    const logoutLinks = document.querySelectorAll('.logout-link');
    logoutLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            logoutRedirect();
        });
    });
});

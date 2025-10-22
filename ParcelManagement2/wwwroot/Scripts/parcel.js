/* ===== JWT 自動掛載 ===== */
(function () {
    const tk = localStorage.getItem('jwt');
    if (tk) {
        axios.defaults.headers.common['Authorization'] = 'Bearer ' + tk;
    }
})();
/* ---------- 共用小工具 ---------- */
function toJson(raw) {
    try { return (typeof raw === 'string') ? JSON.parse(raw) : raw; }
    catch { showMessage('伺服器回傳格式錯誤', 'warn'); return []; }
}
function blockInfo(myJson) {
    if (Array.isArray(myJson) && myJson[0] && myJson[0].msg) {
        if (myJson[0].msg === "FAIL") {
            showMessage(myJson[0].err || "新增失敗", "warn");
            return true;
        }
        switch (myJson[0].msg) {
            case "ZERO": showMessage("查無資料", "warn"); return true;
            case "OUT": ReLogin(); return true;
        }
    }
    return false;
}
// 顯示包裹照片的函數
function showPackagePhoto(photoPath, packId) {
    const uid = Date.now();
    const idBox = `photoModal_${uid}`;
    const modalWidth = Math.min(600, window.innerWidth - 40);
    const modalHeight = Math.min(500, window.innerHeight - 40);
    const modalTop = Math.max(20, (window.innerHeight - modalHeight) / 2);
    const modalLeft = Math.max(20, (window.innerWidth - modalWidth) / 2);

    let contentHtml = '';

    if (photoPath) {
        contentHtml = `
            <img src="${photoPath}" alt="包裹照片" 
                 style="max-width:100%;max-height:100%;object-fit:contain;
                        border-radius:8px;box-shadow:0 2px 10px rgba(0,0,0,0.1);"
                 onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
            <div style="display:none;flex-direction:column;align-items:center;justify-content:center;
                        height:100%;color:#6c757d;">
                <i class="bi bi-box-seam" style="font-size:64px;margin-bottom:15px;color:#0066cc;"></i>
                <p style="margin:0;font-size:16px;">照片載入失敗</p>
                <small style="margin-top:5px;color:#999;">包裹編號: ${packId}</small>
            </div>`;
    } else {
        contentHtml = `
            <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;
                        height:100%;color:#6c757d;">
                <i class="bi bi-box-seam" style="font-size:80px;margin-bottom:20px;color:#0066cc;"></i>
                <p style="margin:0;font-size:18px;font-weight:600;color:#495057;">此包裹暫無照片</p>
                <small style="margin-top:8px;color:#999;font-size:14px;">包裹編號: ${packId}</small>
                <small style="margin-top:3px;color:#999;font-size:12px;">建議收取包裹時拍照記錄</small>
            </div>`;
    }

    const htmlx = `
<div id="${idBox}" style="position:fixed;z-index:10000;
     width:${modalWidth}px; height:${modalHeight}px;
     top:${modalTop}px; left:${modalLeft}px;
     background:white; border-radius:12px;
     box-shadow:0 4px 20px rgba(0,0,0,0.3);
     display:flex; flex-direction:column;">
  
  <div style="background:var(--admin-primary);height:50px;
              display:flex;align-items:center;justify-content:space-between;
              padding:0 20px;border-radius:12px 12px 0 0;color:white;">
      <h5 style="margin:0;font-size:16px;">包裹照片 - ${packId}</h5>
      <button id="closeBtn_${uid}" style="background:none;border:none;color:white;
              font-size:20px;cursor:pointer;padding:0;width:30px;height:30px;
              display:flex;align-items:center;justify-content:center;">×</button>
  </div>
  
  <div style="flex:1;padding:20px;display:flex;align-items:center;justify-content:center;
              background:#f8f9fa;">
        ${contentHtml}
  </div>
 </div>`;

    document.body.insertAdjacentHTML('beforeend', htmlx);

    const modal = document.getElementById(idBox);
    const closeBtn = document.getElementById(`closeBtn_${uid}`);

    // 關閉按鈕事件
    closeBtn.onclick = () => modal.remove();

    // 點擊外部關閉
    modal.onclick = (e) => {
        if (e.target === modal) modal.remove();
    };

    // ESC 鍵關閉
    const handleEsc = (e) => {
        if (e.key === 'Escape') {
            modal.remove();
            document.removeEventListener('keydown', handleEsc);
        }
    };
    document.addEventListener('keydown', handleEsc);
}
function showMessage(message, msgType, fx) {
    setTimeout(()=>{
        const cfg = {
            confirm: { title: '確認', color: 'mediumblue', confirm: 'visible', quit: 'visible', ack: 'hidden' },
            warn: { title: '警告', color: 'orange', confirm: 'hidden', quit: 'hidden', ack: 'visible' },
            success: { title: '成功', color: 'green', confirm: 'hidden', quit: 'hidden', ack: 'visible' }
        }[msgType];
        // 訊息框最大寬度 400px，最小 240px，自動高度
        const msgMaxWidth = 400, msgMinWidth = 240;
        const msgTop = Math.round((window.innerHeight - 200) / 4);
        const msgLeft = Math.round((window.innerWidth - msgMaxWidth) / 2);
        const uid = Date.now();
        const idBox = `msgBox_${uid}`;
        const htmlx = `
<div id="${idBox}" style="position:absolute;z-index:9999;
     max-width:${msgMaxWidth}px; min-width:${msgMinWidth}px; width:auto;
     padding:0; top:${msgTop}px; left:${msgLeft}px; box-shadow:0 2px 12px #0002; border-radius:20px;">
  <div style="background:${cfg.color};height:40px;padding:5px;text-align:center;
              color:#fff;border-top-left-radius:20px;border-top-right-radius:20px;font-size:1.2em;">
      ${cfg.title}
  </div>
  <div style="background:#fff;border:1px solid ${cfg.color};
              padding:20px 24px 10px 24px; font-size:1.1em; word-break:break-all; border-bottom-left-radius:20px; border-bottom-right-radius:20px; width:100%; box-sizing:border-box; min-height:unset; height:auto;">
      <div style="margin-bottom:16px; text-align:center;">${message}</div>
      <div style="display:flex; justify-content:center; align-items:center; width:100%; gap:12px; margin-top:0; flex-wrap:wrap;">
        <button id="btn_quit_${uid}"    class="btn btn-default"
                style="visibility:${cfg.quit}; min-width:64px; margin:0 auto; box-sizing:border-box; display:inline-flex; justify-content:center; align-items:center;">放棄</button>
        <button id="btn_confirm_${uid}" class="btn btn-primary"
                style="background:${cfg.color};visibility:${cfg.confirm}; min-width:64px; margin:0 auto; box-sizing:border-box; display:inline-flex; justify-content:center; align-items:center;">確認</button>
        <button id="btn_ack_${uid}"     class="btn btn-primary"
                style="background:${cfg.color};visibility:${cfg.ack}; min-width:64px; margin:0 auto; box-sizing:border-box; display:inline-flex; justify-content:center; align-items:center;">了解</button>
      </div>
  </div>
</div>`;
        document.body.insertAdjacentHTML('beforeend', htmlx);
        const box = document.getElementById(idBox);
        const quit = document.getElementById(`btn_quit_${uid}`);
        const ack = document.getElementById(`btn_ack_${uid}`);
        const okbtn = document.getElementById(`btn_confirm_${uid}`);
        //了解
        ack && (ack.onclick = () => box.remove());
        //確認
        okbtn && (okbtn.onclick = () => { fx && eval(fx); box.remove(); });
        //放棄
        quit && (quit.onclick = () => box.remove());
    }, 100);
}
/* ---------- Vue ---------- */
const prodObj = Vue.createApp({
    data() {
        return {
            display_edit: false,   //顯示編輯框
            Not_Editable: true,
            //----包裹相關資料----
            Pack_Type: '',
            Red_Id: '',
            selectedPhoto: null,
            showRemarksError: false,
            //----
            ProdTypeList: [],      // 包裹類別清單
            ResidentList: [],      // 住戶清單
            ProductList: [],       // 包裹清單
            QueryOpt: 'ALL',       // 下拉 (ALL / UNDONE / RESIDENT)
            SelectRed: '',       // 住戶下拉選中的 red_id
            SelectStatus: '',       // 新增：包裹狀態
            hasUrlParams: false,    //URL 參數標記
            urlRedId: null,
            urlStatus: null
        }
    },
    created() {
        // 解析 URL 參數
        this.parseUrlParams();

        this.GetAllProductData();
        this.BuildProdTypeList();
        this.BuildResidentList();
    },
    mounted() {
        if (this.hasUrlParams) {
            // 延遲執行，確保資料已載入
            this.$nextTick(() => {
                setTimeout(() => {
                    this.applyUrlParams();
                }, 500); // 給予足夠時間載入資料
            });
        } else {
            this.GetAllProductData();
        }
    },

    methods: {
        // 解析 URL 參數
        parseUrlParams() {
            const url = new URL(window.location.href);
            const redId = url.searchParams.get('red_id');
            const status = url.searchParams.get('status');

            console.log('URL 參數:', { redId, status });

            if (redId || status) {
                this.hasUrlParams = true;
                this.urlRedId = redId;
                this.urlStatus = status;
            }
        },

        applyUrlParams() {
            if (this.urlRedId) {
                this.QueryOpt = 'RESIDENT';
                this.SelectRed = this.urlRedId;

                // 狀態轉換：中文轉數字字串
                if (this.urlStatus) {
                    if (this.urlStatus === '未取') {
                        this.SelectStatus = '0';
                    } else if (this.urlStatus === '已取') {
                        this.SelectStatus = '1';
                    } else {
                        this.SelectStatus = this.urlStatus;
                    }
                } else {
                    this.SelectStatus = '';
                }
                // 執行搜尋
                this.SearchByResident();

                //清除 URL 參數
                // window.history.replaceState({}, '', window.location.pathname);
            }
        },
        getStatusText(status) {
            return (status == 0 || status === '0') ? '未取' : '已取';
        },

        // 取得狀態CSS類別
        getStatusClass(status) {
            return (status == 0 || status === '0') ? 'package-status-pending' : 'package-status-collected';
        },

        //取得標示CSS類別
        getBadgeClass(status) {
            const baseClass = 'badge rounded-pill fs-6 py-2 px-3 ';
            const statusClass = (status == 0 || status === '0')
                ? 'package-status-pending status-badge-pending'
                : 'package-status-collected status-badge-collected';
            return `${baseClass} ${statusClass}`;
        },
        // 處理照片選擇
        handlePhotoSelect(event) {
            const file = event.target.files[0];
            if (file) {
                // 檢查檔案類型
                const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif'];
                if (!allowedTypes.includes(file.type)) {
                    showMessage("請選擇 JPG、PNG 或 GIF 格式的圖片", "warn");
                    event.target.value = '';
                    this.selectedPhoto = null;
                    return;
                }

                // 檢查檔案大小 (5MB)
                if (file.size > 5 * 1024 * 1024) {
                    showMessage("圖片大小不能超過 5MB", "warn");
                    event.target.value = '';
                    this.selectedPhoto = null;
                    return;
                }

                this.selectedPhoto = file;
            }
        },
        // 查看包裹照片
        viewPackagePhoto(item) {
            const uid = Date.now();
            const idBox = `photoModal_${uid}`;
            const modalWidth = Math.min(600, window.innerWidth - 40);
            const modalHeight = Math.min(500, window.innerHeight - 40);
            const modalTop = Math.max(20, (window.innerHeight - modalHeight) / 2);
            const modalLeft = Math.max(20, (window.innerWidth - modalWidth) / 2);

            let contentHtml = '';

            if (item.photo_path) {
                contentHtml = `
            <img src="${item.photo_path}" alt="包裹照片" 
                 style="max-width:100%;max-height:100%;object-fit:contain;
                        border-radius:8px;box-shadow:0 2px 10px rgba(0,0,0,0.1);"
                 onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
            <div style="display:none;flex-direction:column;align-items:center;justify-content:center;
                        height:100%;color:#6c757d;">
                <i class="bi bi-box-seam" style="font-size:64px;margin-bottom:15px;color:#0066cc;"></i>
                <p style="margin:0;font-size:16px;">照片載入失敗</p>
                <small style="margin-top:5px;color:#999;">包裹編號: ${item.pack_id}</small>
            </div>`;
            } else {
                contentHtml = `
            <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;
                        height:100%;color:#6c757d;">
                <i class="bi bi-box-seam" style="font-size:80px;margin-bottom:20px;color:#0066cc;"></i>
                <p style="margin:0;font-size:18px;font-weight:600;color:#495057;">此包裹暫無照片</p>
                <small style="margin-top:8px;color:#999;font-size:14px;">包裹編號: ${item.pack_id}</small>
                <small style="margin-top:3px;color:#999;font-size:12px;">建議收取包裹時拍照記錄</small>
            </div>`;
            }
            const htmlx = `
        <div id="${idBox}" style="position:fixed;z-index:10000;
             width:${modalWidth}px; height:${modalHeight}px;
             top:${modalTop}px; left:${modalLeft}px;
             background:white; border-radius:12px;
             box-shadow:0 4px 20px rgba(0,0,0,0.3);
             display:flex; flex-direction:column;">
          
          <div style="background:var(--admin-primary);height:50px;
                      display:flex;align-items:center;justify-content:space-between;
                      padding:0 20px;border-radius:12px 12px 0 0;color:white;">
              <h5 style="margin:0;font-size:16px;">包裹照片 - ${item.pack_id}</h5>
              <button id="closeBtn_${uid}" style="background:none;border:none;color:white;
                      font-size:20px;cursor:pointer;padding:0;width:30px;height:30px;
                      display:flex;align-items:center;justify-content:center;">×</button>
          </div>
          
          <div style="flex:1;padding:20px;display:flex;align-items:center;justify-content:center;
                      background:#f8f9fa;">
              ${contentHtml}
          </div>
          
        </div>`;
            document.body.insertAdjacentHTML('beforeend', htmlx);

            const modal = document.getElementById(idBox);
            const closeBtn = document.getElementById(`closeBtn_${uid}`);
            // 關閉按鈕事件
            closeBtn.onclick = () => modal.remove();
            // 點擊外部關閉
            modal.onclick = (e) => {
                if (e.target === modal) modal.remove();
            };
            // ESC 鍵關閉
            const handleEsc = (e) => {
                if (e.key === 'Escape') {
                    modal.remove();
                    document.removeEventListener('keydown', handleEsc);
                }
            };
            document.addEventListener('keydown', handleEsc);
        },
        /* === 把後端回來的 1 筆資料轉成畫面需要的格式 === */
        convertRec(r) {
            let statusValue = r.status;
            if (typeof statusValue === 'string') {
                statusValue = parseInt(statusValue, 10);
            }
            if (isNaN(statusValue)) {
                statusValue = 0; // 預設為未取
            }
            let remarksValue = r.remarks;
            if (remarksValue === undefined ||
                remarksValue === null ||
                remarksValue === '' ||
                String(remarksValue).trim() === '' ||
                remarksValue === 'null') {
                remarksValue = '-';
            } else {
                remarksValue = String(remarksValue).trim();
            }
            console.log('convertRec - remarks:', { original: r.remarks, converted: remarksValue });
            return {
                pack_id: r.pack_id,       // 顯示用
                pack_name: r.pack_name,
                red_name: r.red_name,
                condo_id: r.condo_id,
                status: statusValue,
                pickup_datetime: r.pickup_datetime || '',
                remarks: remarksValue,
                photo_path: r.photo_path || '',
                Pack_Id: r.pack_id,       // 後端需要
                Pack_Type: r.pack_type,
                Flag_Update: false,
                Flag_Del: false
            };
        },
        onUpdateChk(item) {
            if (item.Flag_Update) item.Flag_Del = false; // 如果更新被勾選，則取消刪除勾選
        },
        onDeleteChk(item) {
            if (item.Flag_Del) item.Flag_Update = false; // 如果刪除被勾選，則取消更新勾選
        },
        // 建立包裹類別清單
        BuildProdTypeList() {
            axios.post("/Home/BuildProdTypeList")
                .then(res => {
                    console.log("BuildProdTypeList response:", res.data);
                    const js = toJson(res.data);
                    if (blockInfo(js) === false) {
                        this.ProdTypeList = js;
                        console.log("ProdTypeList set to:", this.ProdTypeList);
                    }
                }).catch(() => showMessage("連線錯誤", "warn"));
        },
        // 建立住戶清單
        BuildResidentList() {
            axios.post("/Home/BuildResidentList")
                .then(res => {
                    console.log("BuildResidentList response:", res.data);
                    const js = toJson(res.data);
                    if (blockInfo(js) === false) {
                        this.ResidentList = js;
                        console.log("ResidentList set to:", this.ResidentList);
                    }
                }).catch(() => showMessage("連線錯誤", "warn"));
        },
        // 取得所有包裹資料
        GetAllProductData() {
            console.log('載入所有包裹...');
            axios.post("/Home/GetAllProduct")
                .then(res => {
                    console.log('GetAllProduct response:', res.data);
                    const js = toJson(res.data);
                    if (blockInfo(js) === false) {
                        if (Array.isArray(js) && js.length > 0) {
                            this.ProductList = js.map(r => this.convertRec(r));
                        } else {
                            this.ProductList = [];
                        }
                        console.log('ProductList set to:', this.ProductList);
                    } else {
                        this.ProductList = [];
                    }
                })
                .catch(() => {
                    this.ProductList = [];
                    showMessage("載入包裹失敗", "warn");
            });
        },
        // 查詢未領取包裹
        GetUndeliveredPackages() {
            console.log('載入未領取包裹...');
            axios.post("/Home/GetUndeliveredPackages")
                .then(res => {
                    console.log('GetUndeliveredPackages response:', res.data);
                    const js = toJson(res.data);
                    if (blockInfo(js) === false) {
                        if (Array.isArray(js) && js.length > 0) {
                            this.ProductList = js.map(r => this.convertRec(r));
                        } else {
                            this.ProductList = [];
                        }
                        console.log('ProductList set to:', this.ProductList);
                    } else {
                        this.ProductList = [];
                    }
                })
                .catch(() => {
                    this.ProductList = [];
                    showMessage('載入未領取包裹失敗', 'warn');
                });
        },
        /* ==== 住戶下拉選取 ==== */
        SearchByResident() {
            if (this.SelectRed === '') return;
            axios.post('/Home/GetPackagesByResident', {
                redId: this.SelectRed,
                status: this.SelectStatus
            })
                .then(res => {
                    const js = toJson(res.data);
                    if (blockInfo(js) === false && Array.isArray(js)) {
                        this.ProductList = js.map(r => this.convertRec(r));
                    } else {
                        this.ProductList = [];
                    }
                })
                .catch(() => showMessage("連線錯誤", "warn"));
        },
        /* ==== 功能下拉切換 ==== */
        HandleQuery() {
            this.ProductList = [];
            if (!this.hasUrlParams) {
                this.SelectRed = '';
                this.SelectStatus = '';
            }

            if (this.QueryOpt === 'ALL') { this.GetAllProductData(); }
            else if (this.QueryOpt === 'UNDONE') { this.GetUndeliveredPackages(); }
            else if (this.QueryOpt === 'RESIDENT') { this.ProductList = []; }   // 選 RESIDENT → 等待人選
            else if (this.QueryOpt === 'HISTORY') {
                this.display_edit = false; this.GetDeleted();
            }
            this.hasUrlParams = false;
        },
        // 新增包裹
        AddProduct() {
            this.showRemarksError = false;
            if (this.Pack_Type === '' || this.Red_Id === '') {
                showMessage("類別及住戶必選", "warn");
                return;
            }
            if (!this.Remarks || this.Remarks.trim() === '') {
                this.showRemarksError = true;
                showMessage('請填寫備註內容，至少需要註明包裹放置位置！', "warn");
                return;
            }
            console.log('準備新增包裹:', {
                Pack_Type: this.Pack_Type,
                Red_Id: this.Red_Id,
                Remarks: this.Remarks, 
                selectedPhoto: this.selectedPhoto
            });
            // 創建 FormData 對象以支持文件上傳
            const formData = new FormData();
            formData.append('Pack_Type', this.Pack_Type);
            formData.append('Red_Id', this.Red_Id);
            formData.append('Remarks', this.Remarks || '');

            if (this.selectedPhoto) {
                formData.append('photo', this.selectedPhoto);
            }
            for (let pair of formData.entries()) {
                console.log(pair[0] + ': ' + pair[1]);
            }

            // 使用 FormData 上傳
            axios.post('/Home/AddProduct', formData, {
                headers: {
                    'Content-Type': 'multipart/form-data'
                }
            }).then(res => {
                const js = res.data;
                if (blockInfo(js)) return;
                if (js[0].msg === 'OK') {
                    showMessage("包裹已新增", "success");
                    this.GetAllProductData();
                    this.Pack_Type = this.Red_Id = this.Remarks = '';
                    this.showRemarksError = false;
                    this.selectedPhoto = null;
                    // 清除文件選擇器
                    const fileInput = document.getElementById('photoUpload');
                    if (fileInput) fileInput.value = '';
                }
            }).catch(() => showMessage("連線錯誤", "warn"));
        },
        clearRemarksError() {
            if (this.showRemarksError && this.Remarks && this.Remarks.trim() !== '') {
                this.showRemarksError = false;
            }
        },
        // 取刪除歷史
        GetDeleted() {
            this.historyLoading = true;
            axios.post('/Home/GetDeletedPackages')
                .then(res => {
                    const js = toJson(res.data);
                    if (blockInfo(js) === false) {
                        if (Array.isArray(js) && js.length > 0) {
                            this.ProductList = js.map(r => this.convertRec(r));
                            console.log('歷史紀錄載入成功，共', this.ProductList.length, '筆');
                        }
                        else {
                            this.ProductList = [];
                            console.log('歷史紀錄無資料');
                        }
                    }
                    else {
                        this.ProductList = [];
                        console.log('歷史紀錄查詢失敗');
                    }
                })
                .catch(error => {
                    console.error('載入歷史紀錄失敗:', error);
                    this.ProductList = [];
                    showMessage('載入歷史紀錄失敗', 'warn');
                })
        },
        /* ====== 歷史單筆復原 ====== */
        RestoreOne(row) {
            if (!confirm(`確定復原 ${row.pack_id} 嗎？`)) return;
            axios.post('/Home/RestorePackage',
                { id: row.pack_id },
                {
                    headers: { 'Content-Type': 'application/json' }
                }
            )
            .then(res => {
                const js = toJson(res.data);
                if (blockInfo(js)) {
                    return;
                }
                showMessage('已復原', 'success');
                // 依目前頁籤刷新
                if (this.QueryOpt === 'HISTORY') {
                    this.GetDeleted();
                } else if (this.QueryOpt === 'ALL') {
                    this.GetAllProductData();
                } else if (this.QueryOpt === 'UNDONE') {
                    this.GetUndeliveredPackages();
                } else if (this.QueryOpt === 'RESIDENT') {
                    this.SearchByResident();
                }
            })
            .catch(error => {
                console.error('復原包裹失敗:', error);
                showMessage('連線錯誤', 'warn');
            });
        },
        /* ====== 歷史單筆清除 ====== */
        RemoveOne(row) {
            if (!confirm(`確定要永久清除 ${row.pack_id} 嗎？此操作無法復原！`)) return;
            axios.post('/Home/RemovePackage', { id: row.pack_id })
                .then(res => {
                    if (blockInfo(res.data) !== false) {
                        return;
                    }
                    showMessage('已清除', 'success');

                    if (this.QueryOpt === 'HISTORY') {
                        this.GetDeleted();
                    }
                })
                .catch(() => showMessage('連線錯誤', 'warn'));
        },
        // ---- 新增批次儲存 ----
        UpdatePackages() {
            // 收集被勾選資料
            const rows = this.ProductList.filter(r => r.Flag_Update || r.Flag_Del);
            if (rows.length === 0) {
                showMessage("請先勾選要更新的資料", "warn"); return;
            }
            if (rows.some(r => r.Flag_Del) && !confirm("有勾選刪除資料，確定要刪除嗎？")) {
                    //取消並復原所有
                    this.ProductList.forEach(r => r.Flag_Del = false);
                    return;
            }
            const payload = rows.map(r => ({
                Flag_Update: r.Flag_Update,
                Flag_Del: r.Flag_Del,
                Pack_Id: r.Pack_Id,
                Pack_Type: r.Pack_Type,
                Status: r.status
            }));
            axios.post('/Home/BatchUpdateProduct', payload) 
                .then(res => {
                    if (blockInfo(res.data) !== false) return;
                    showMessage("更新完成", "success");
                    // 依目前查詢條件刷新
                    if (this.QueryOpt === 'ALL') this.GetAllProductData();
                    else if (this.QueryOpt === 'UNDONE') this.GetUndeliveredPackages();
                    else if (this.QueryOpt === 'RESIDENT') this.SearchByResident();
                })
                .catch(() => showMessage("連線錯誤", "warn"));
        },
        getPackageTypeClass(item) {
            const packName = (item.pack_name || item.packname || '').toString();

            // 判斷是否為信件類型
            if (packName.includes('信件')) {
                return 'package-type-letter';
            }
            return 'package-type-parcel';
        },
        // 顯示/隱藏編輯權限
        ShowEdit() {
            this.Not_Editable = !this.display_edit;
        }
    }
}).mount('#ProductItem');

/* 登出工具*/
function logout() {
    localStorage.removeItem('jwt');
    delete axios.defaults.headers.common.Authorization;
    location.href = '/Account/Login';
}

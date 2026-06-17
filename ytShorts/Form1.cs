using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace ytShorts
{
    public partial class Form1 : Form, IMessageFilter
    {
        private Panel topBar;
        private Button btnClose;
        private WebView2 webView;
        private System.Windows.Forms.Timer uiTimer;

        // Pencere Derinliği (Z-Order) Kontrolü için Windows API'leri
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);     // En arka plan
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);   // En ön plan
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;

        // Sürükleme API'leri
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public Form1()
        {
            ArayuzuHazirla();
            TarayiciyiBaslat();
            Application.AddMessageFilter(this); // Form odaktayken 'Ö' tuşunu yakalamak için
        }

        // 1. DURUM: Odak Formun üzerindeyken 'Ö' tuşunu yakalayan filtre
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_KEYDOWN = 0x0100;
            if (m.Msg == WM_KEYDOWN)
            {
                // 188 değeri standart Türkçe Q klavyede 'Ö' tuşunun sanal key kodudur (Keys.Oemcomma)
                if ((int)m.WParam == 188)
                {
                    Application.Exit();
                    return true;
                }
            }
            return false;
        }

        private void ArayuzuHazirla()
        {
            this.Text = "Background Worker";
            this.Size = new Size(135, 240);
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(28, 28, 28);

            // Sabit Saydamlık (Hayalet Modu)
            this.Opacity = 0.25;
            this.ShowInTaskbar = true;

            // Ekranın sağ alt köşesinde başlat
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(workingArea.Right - this.Width - 20, workingArea.Bottom - this.Height - 20);

            // Üst Bar
            topBar = new Panel();
            topBar.Height = 14;
            topBar.Dock = DockStyle.Top;
            topBar.BackColor = Color.FromArgb(45, 45, 45);
            topBar.MouseDown += TopBar_MouseDown;

            // Kapatma Butonu
            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Dock = DockStyle.Right;
            btnClose.Width = 18;
            btnClose.Font = new Font("Arial", 5, FontStyle.Bold);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.Red;
            btnClose.ForeColor = Color.LightGray;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => Application.Exit();

            topBar.Controls.Add(btnClose);
            this.Controls.Add(topBar);

            // WebView2 Player
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            webView.NavigationCompleted += WebView_NavigationCompleted;
            this.Controls.Add(webView);

            // Z-Order Kontrolü için Timer
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 100;
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            Point mousePos = Cursor.Position;
            bool isMouseOver = this.Bounds.Contains(mousePos);

            if (isMouseOver)
            {
                if (!this.TopMost)
                {
                    this.TopMost = true;
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            else
            {
                if (this.TopMost)
                {
                    this.TopMost = false;
                    SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        private void TopBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private async void TarayiciyiBaslat()
        {
            // 1. Çerezlerin ve hesabın kalıcı olacağı özel bir klasör yolu belirliyoruz
            // Bu klasör C:\Users\KullaniciAdi\AppData\Local\ytShortsPiP_Data konumunda gizli duracak
            string userDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ytShortsPiP_Data");

            // 2. WebView2'ye bu klasörü kullanmasını söylüyoruz
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);

            // 3. Çekirdeği bu kalıcı profil ile başlatıyoruz
            await webView.EnsureCoreWebView2Async(env);

            // "Ö" tuşu ile kapatma JavaScript'imiz (Burası aynı kalıyor)
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                document.addEventListener('keydown', function(e) {
                    if (e.key === 'ö' || e.key === 'Ö') {
                        window.chrome.webview.postMessage('sistemi_kapat');
                    }
                });
            ");

            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // 4. YouTube'a git
            webView.Source = new Uri("https://www.youtube.com/shorts");
        }

        private void CoreWebView2_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Eğer gelen mesaj bizim JavaScript'ten yolladığımız 'sistemi_kapat' ise programı sonlandır
            if (e.TryGetWebMessageAsString() == "sistemi_kapat")
            {
                Application.Exit();
            }
        }

        private void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            webView.ZoomFactor = 0.55;
        }
    }
}
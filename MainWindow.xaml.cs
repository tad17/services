using System;
using System.Diagnostics; // Добавлен для Debug.WriteLine
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace services // Должно соответствовать папке проекта
{
    public partial class MainWindow : Window
    {
        // --- P/Invoke для Windows API ---
        // ... (оставьте этот раздел без изменений, как в предыдущем рабочем коде) ...
        [DllImport("user32.dll")]
        internal static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int GWL_STYLE = -16;
        const uint WS_SYSMENU = 0x00080000;
        const uint WS_CAPTION = 0x00C00000;
        const uint WS_BORDER = 0x00800000;
        const int WS_POPUP = unchecked((int)0x80000000);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_FRAMECHANGED = 0x0020;

        // --- Переменные и состояние ---
        private double screenWidth;
        private double screenHeight;
        // ИЗМЕНЕНО: Фиксированная ширина панели
        private const double PanelWidth = 500;
        // ИЗМЕНЕНО: Высота панели (половина экрана)
        private double panelHeight; // Будет установлено в Loaded

        private const double tabWidth = 20; // Ширина видимого "таба"
        private bool isPanelShown = false;
        private bool isAnimating = false;

        private Storyboard? showPanelStoryboard;
        private Storyboard? hidePanelStoryboard;

        private const double animationDuration = 300;

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- Обработчик загрузки окна ---
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow_Loaded: Окно загружено.");

            var hwnd = new WindowInteropHelper(this).Handle;
            Debug.WriteLine($"MainWindow_Loaded: HWND окна: {hwnd}");

            // Устанавливаем стиль окна (без декораций)
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, unchecked((int)(currentStyle & ~(WS_CAPTION | WS_SYSMENU | WS_BORDER) | WS_POPUP)));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
             Debug.WriteLine("MainWindow_Loaded: Стиль окна установлен.");


            // Устанавливаем Z-порядок (всегда поверх)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
             Debug.WriteLine("MainWindow_Loaded: Окно установлено как TOPMOST.");


            // Получаем размер основного экрана
            screenWidth = SystemParameters.PrimaryScreenWidth;
            screenHeight = SystemParameters.PrimaryScreenHeight;
            Debug.WriteLine($"MainWindow_Loaded: Размер экрана: {screenWidth}x{screenHeight}");

            // ИЗМЕНЕНО: Устанавливаем размер окна (фиксированная ширина, половина высоты экрана)
            panelHeight = screenHeight / 2.0;
            this.Width = PanelWidth; // Устанавливаем фиксированную ширину
            this.Height = panelHeight; // Устанавливаем половину высоты экрана
            Debug.WriteLine($"MainWindow_Loaded: Размер окна установлен: {this.Width}x{this.Height}");

            // ИЗМЕНЕНО: Устанавливаем начальную позицию (скрыто за правым краем, виден только таб)
            // Левый край окна находится на screenWidth - tabWidth.
            // При этом правый край окна будет на screenWidth - tabWidth + PanelWidth.
            // Так как PanelWidth (500) > tabWidth (20), большая часть окна будет скрыта справа.
            // Видимая часть (таб) будет область [screenWidth - tabWidth, screenWidth]
            this.Left = screenWidth - tabWidth; // Позиция левого края окна
            this.Top = screenHeight / 2.0 - panelHeight / 2.0; // Центрируем по вертикали

            Debug.WriteLine($"MainWindow_Loaded: Начальная позиция Left: {this.Left}, Top: {this.Top}");

            // Настраиваем анимации
            SetupAnimations();
             Debug.WriteLine("MainWindow_Loaded: Анимации настроены.");

            // Окно уже показано WPF, теперь оно сдвинуто и поверх других.
            // Если окно не видно, проверьте, нет ли ошибок в Output окне Visual Studio
            // или при запуске из командной строки (debug вывод может там отображаться)
        }

        // --- Настройка анимаций ---
        private void SetupAnimations()
        {
            // ИЗМЕНЕНО: Анимация показа (сдвиг влево)
            // Начинаем с позиции, где виден только таб: screenWidth - tabWidth
            // Заканчиваем, когда вся панель видна у правого края: screenWidth - PanelWidth
            var showAnimation = new DoubleAnimation
            {
                From = screenWidth - tabWidth, // Начальная позиция (скрыто, виден таб)
                To = screenWidth - PanelWidth, // Конечная позиция (полностью видно)
                Duration = TimeSpan.FromMilliseconds(animationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            showPanelStoryboard = new Storyboard();
            Storyboard.SetTarget(showAnimation, this);
            Storyboard.SetTargetProperty(showAnimation, new PropertyPath(Window.LeftProperty));
            showPanelStoryboard.Children.Add(showAnimation);
            showPanelStoryboard.Completed += (s, e) =>
            {
                isPanelShown = true;
                isAnimating = false;
                Debug.WriteLine($"Анимация показа завершена. Позиция Left: {this.Left}");
            };


            // ИЗМЕНЕНО: Анимация скрытия (сдвиг вправо)
            // Начинаем с полностью показанной позиции: screenWidth - PanelWidth
            // Заканчиваем в позиции, где виден только таб: screenWidth - tabWidth
            var hideAnimation = new DoubleAnimation
            {
                From = screenWidth - PanelWidth, // Начальная позиция (полностью видно)
                To = screenWidth - tabWidth, // Конечная позиция (скрыто, виден таб)
                Duration = TimeSpan.FromMilliseconds(animationDuration),
                 EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            hidePanelStoryboard = new Storyboard();
            Storyboard.SetTarget(hideAnimation, this);
            Storyboard.SetTargetProperty(hideAnimation, new PropertyPath(Window.LeftProperty));
            hidePanelStoryboard.Children.Add(hideAnimation);
            hidePanelStoryboard.Completed += (s, e) =>
            {
                isPanelShown = false;
                isAnimating = false;
                 Debug.WriteLine($"Анимация скрытия завершена. Позиция Left: {this.Left}");
            };
        }

        // --- Обработка движения мыши по окну ---
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // Если анимация идет, игнорируем события мыши
            if (isAnimating) return;

            // Получаем позицию курсора относительно верхнего левого угла окна
            Point mousePositionRelativeToWindow = e.GetPosition(this);

            // Вычисляем абсолютные координаты курсора на экране
            double absoluteMouseX = this.Left + mousePositionRelativeToWindow.X;
            double absoluteMouseY = this.Top + mousePositionRelativeToWindow.Y;

            // Debug.WriteLine($"MouseMove: Абс. X={absoluteMouseX}, Абс. Y={absoluteMouseY}. Окно Left={this.Left}, Top={this.Top}. Показана={isPanelShown}, Анимируется={isAnimating}");

            // --- Логика показа при наведении на таб ---
            // Панель скрыта (ее левый край на screenWidth - tabWidth)
            // И курсор находится над правой tabWidth пикселями экрана.
            // Т.е., абсолютная X координата мыши больше чем screenWidth - tabWidth.
            if (!isPanelShown && !isAnimating && this.Left == screenWidth - tabWidth && absoluteMouseX > screenWidth - tabWidth)
            {
                // Условие this.Left == screenWidth - tabWidth проверяет, что окно точно в скрытом состоянии.
                Debug.WriteLine($"Наведение на таб. Абс. X: {absoluteMouseX}, Край экрана для таба: {screenWidth - tabWidth}");
                ShowPanel();
            }

            // --- ИЗМЕНЕНО: Логика скрытия при уходе мыши за пределы окна ---
            // Панель показана
            // И курсор находится ЗА пределами прямоугольника окна
            if (isPanelShown && !isAnimating)
            {
                // Проверяем, находится ли курсор вне границ окна:
                // Слева от левого края ИЛИ справа от правого края ИЛИ выше верхнего края ИЛИ ниже нижнего края
                if (absoluteMouseX < this.Left || absoluteMouseX > this.Left + this.Width ||
                    absoluteMouseY < this.Top || absoluteMouseY > this.Top + this.Height)
                {
                     Debug.WriteLine("Мышь покинула границы окна. Скрываем панель.");
                    HidePanel(); // Скрываем панель
                }
            }
        }

        // --- Методы показа/скрытия панели ---
         private void ShowPanel()
        {
            if (showPanelStoryboard == null || hidePanelStoryboard == null) // Проверка инициализации Storyboard
            {
                 Debug.WriteLine("ShowPanel: Анимации не настроены!");
                 return;
            }

            if (isPanelShown || isAnimating) return;
            isAnimating = true;
            Debug.WriteLine("ShowPanel: Запуск анимации показа");
            hidePanelStoryboard.Stop(this);
            showPanelStoryboard.Begin(this);
        }

        private void HidePanel()
        {
            if (showPanelStoryboard == null || hidePanelStoryboard == null) // Проверка инициализации Storyboard
            {
                Debug.WriteLine("HidePanel: Анимации не настроены!");
                return;
            }

             if (!isPanelShown || isAnimating) return;
             isAnimating = true;
             Debug.WriteLine("HidePanel: Запуск анимации скрытия");
             showPanelStoryboard.Stop(this);
             hidePanelStoryboard.Begin(this);
        }


        // --- Обработчики кнопок (сервисные функции) ---
         private void LaunchNotepad_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Нажата кнопка Открыть Блокнот");
            LaunchApplication("notepad.exe");
            HidePanel();
        }

        private void LaunchCalculator_Click(object sender, RoutedEventArgs e)
        {
             Debug.WriteLine("Нажата кнопка Открыть Калькулятор");
             LaunchApplication("calc.exe");
             HidePanel();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Нажата кнопка Выход");
            App.Current.Shutdown();
        }

        // --- Вспомогательный метод для запуска программ ---
        private void LaunchApplication(string appName)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка запуска {appName}: {ex.Message}");
            }
        }
    }
}

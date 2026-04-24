using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace StarsCalendar
{
    public partial class MainPage : ContentPage
    {
        // Хранилище звёздочек: ключ = дата (без времени), значение = количество звёзд
        private Dictionary<DateTime, int> _stars = new();

        private DateTime _currentMonth = DateTime.Today; // Отображаемый месяц

        public MainPage()
        {
            InitializeComponent();
            LoadStars();          // загружаем сохранённые звёзды
            BuildCalendar();      // строим сетку календаря
            UpdateMonthLabel();   // обновляем заголовок месяца
        }

        // ------------------ Работа с хранилищем ------------------
        private void LoadStars()
        {
            string json = Preferences.Get("stars_data", "{}");
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<DateTime, int>>(json);
                if (loaded != null)
                    _stars = loaded;
            }
            catch
            {
                _stars = new Dictionary<DateTime, int>();
            }
        }

        private void SaveStars()
        {
            string json = JsonSerializer.Serialize(_stars);
            Preferences.Set("stars_data", json);
        }

        // ------------------ Логика добавления звёзд ------------------
        private void AddStarForDate(DateTime date)
        {
            // Приводим дату к началу дня (без времени)
            var key = date.Date;
            if (_stars.ContainsKey(key))
                _stars[key]++;
            else
                _stars[key] = 1;

            SaveStars();
            RefreshCalendar();    // обновляем отображение звёзд в календаре
        }

        // ------------------ Построение календаря ------------------
        private void BuildCalendar()
        {
            // Очищаем сетку
            CalendarGrid.Children.Clear();
            CalendarGrid.RowDefinitions.Clear();
            CalendarGrid.ColumnDefinitions.Clear();

            // 7 столбцов (пн, вт, ср, чт, пт, сб, вс)
            for (int i = 0; i < 7; i++)
                CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            // Заголовки дней недели
            string[] dayNames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
            // Переупорядочим, если неделя начинается с понедельника (CultureInfo может начинать с воскресенья)
            int firstDayOfWeek = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            for (int col = 0; col < 7; col++)
            {
                int dayIndex = (firstDayOfWeek + col) % 7;
                var header = new Label
                {
                    Text = dayNames[dayIndex],
                    HorizontalOptions = LayoutOptions.Center,
                    FontAttributes = FontAttributes.Bold,
                    BackgroundColor = Colors.LightGray
                };
                Grid.SetRow(header, 0);
                Grid.SetColumn(header, col);
                CalendarGrid.Children.Add(header);
            }

            // Определяем количество строк (заголовок + до 6 недель)
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // строка заголовков

            // Получаем первый день месяца
            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            // Смещение до первого дня в сетке (0 = понедельник, 6 = воскресенье)
            int offset = (7 + (firstDayOfMonth.DayOfWeek - CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)) % 7;

            DateTime currentDate = firstDayOfMonth.AddDays(-offset);
            int row = 1;

            while (currentDate.Month <= _currentMonth.Month || currentDate.DayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
            {
                // Добавляем новую строку, если нужно
                if (CalendarGrid.RowDefinitions.Count <= row)
                    CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                for (int col = 0; col < 7; col++)
                {
                    // Создаём ячейку для даты
                    var dayBox = CreateDayCell(currentDate);
                    Grid.SetRow(dayBox, row);
                    Grid.SetColumn(dayBox, col);
                    CalendarGrid.Children.Add(dayBox);

                    currentDate = currentDate.AddDays(1);
                }
                row++;
            }
        }

        // Создаёт визуальный элемент для одного дня (Frame с датой и звёздами)
        private Frame CreateDayCell(DateTime date)
        {
            bool isCurrentMonth = date.Month == _currentMonth.Month;
            int starCount = _stars.ContainsKey(date.Date) ? _stars[date.Date] : 0;

            // Содержимое ячейки: вертикальный стек с числом и звёздами
            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center
            };

            // Число месяца
            var dayLabel = new Label
            {
                Text = date.Day.ToString(),
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = isCurrentMonth ? Colors.Black : Colors.Gray
            };
            stack.Children.Add(dayLabel);

            // Отображение звёздочек (⭐ повторяется starCount раз, или текст с количеством)
            if (starCount > 0)
            {
                var starsLabel = new Label
                {
                    Text = new string('⭐', Math.Min(starCount, 5)) + (starCount > 5 ? $" +{starCount - 5}" : ""),
                    FontSize = 12,
                    HorizontalOptions = LayoutOptions.Center
                };
                stack.Children.Add(starsLabel);
            }
            else
            {
                var emptyLabel = new Label
                {
                    Text = " ",
                    FontSize = 12
                };
                stack.Children.Add(emptyLabel);
            }

            // Оформление ячейки
            var frame = new Frame
            {
                Content = stack,
                BorderColor = isCurrentMonth ? Colors.LightBlue : Colors.LightGray,
                CornerRadius = 5,
                Padding = 5,
                BackgroundColor = (date.Date == DateTime.Today) ? Colors.LightYellow : Colors.White
            };

            // При клике на ячейку можно показать информацию (опционально)
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => InfoLabel.Text = $"{date:dd.MM.yyyy}: {starCount} звёзд";
            frame.GestureRecognizers.Add(tapGesture);

            return frame;
        }

        // Обновляет только содержимое ячеек (без перестроения всей сетки)
        private void RefreshCalendar()
        {
            // Перебираем все дочерние элементы CalendarGrid, начиная с индекса 7 (первые 7 — заголовки)
            int childIndex = 7; // первые 7 элементов — заголовки дней
            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int offset = (7 + (firstDayOfMonth.DayOfWeek - CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)) % 7;
            DateTime currentDate = firstDayOfMonth.AddDays(-offset);

            while (childIndex < CalendarGrid.Children.Count)
            {
                if (CalendarGrid.Children[childIndex] is Frame frame && frame.Content is VerticalStackLayout stack)
                {
                    // Обновляем звёздочки
                    int starCount = _stars.ContainsKey(currentDate.Date) ? _stars[currentDate.Date] : 0;
                    if (stack.Children.Count >= 2 && stack.Children[1] is Label starsLabel)
                    {
                        starsLabel.Text = starCount > 0
                            ? new string('⭐', Math.Min(starCount, 5)) + (starCount > 5 ? $" +{starCount - 5}" : "")
                            : " ";
                    }

                    // Обновляем цвет фона для сегодняшнего дня
                    frame.BackgroundColor = (currentDate.Date == DateTime.Today) ? Colors.LightYellow : Colors.White;
                }
                currentDate = currentDate.AddDays(1);
                childIndex++;
            }
        }

        // ------------------ Навигация по месяцам ------------------
        private void UpdateMonthLabel()
        {
            MonthYearLabel.Text = _currentMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        }

        private void OnPrevMonthClicked(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateMonthLabel();
            BuildCalendar(); // перестраиваем сетку для нового месяца
        }

        private void OnNextMonthClicked(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateMonthLabel();
            BuildCalendar();
        }

        // ------------------ Обработчик кнопки "Добавить звезду за сегодня" ------------------
        private void OnAddStarClicked(object sender, EventArgs e)
        {
            AddStarForDate(DateTime.Today);
            InfoLabel.Text = $"⭐ Добавлена звезда за {DateTime.Today:dd.MM.yyyy}";
        }
    }
}
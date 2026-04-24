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

        // ------------------ Логика изменения звёзд ------------------
        private void SetStarsForDate(DateTime date, int newCount)
        {
            var key = date.Date;
            if (newCount <= 0)
                _stars.Remove(key);
            else
                _stars[key] = newCount;

            SaveStars();
            RefreshCalendar();
        }

        private void AddOneStar(DateTime date)
        {
            var key = date.Date;
            int current = _stars.ContainsKey(key) ? _stars[key] : 0;
            SetStarsForDate(date, current + 1);
        }

        private void SubtractOneStar(DateTime date)
        {
            var key = date.Date;
            int current = _stars.ContainsKey(key) ? _stars[key] : 0;
            if (current > 0)
                SetStarsForDate(date, current - 1);
        }

        private async Task ChangeStarsViaDialog(DateTime date)
        {
            var key = date.Date;
            int current = _stars.ContainsKey(key) ? _stars[key] : 0;
            string result = await DisplayPromptAsync(
                "Количество звёзд",
                $"Введите количество звёзд для {date:dd.MM.yyyy}:",
                initialValue: current.ToString(),
                keyboard: Keyboard.Numeric
            );

            if (int.TryParse(result, out int newCount) && newCount >= 0)
            {
                SetStarsForDate(date, newCount);
                InfoLabel.Text = $"⭐ {date:dd.MM.yyyy}: теперь {newCount} звёзд";
            }
            else if (result != null)
            {
                await DisplayAlert("Ошибка", "Введите целое неотрицательное число", "OK");
            }
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

            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int offset = (7 + (firstDayOfMonth.DayOfWeek - CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)) % 7;

            DateTime currentDate = firstDayOfMonth.AddDays(-offset);
            int row = 1;

            while (currentDate.Month <= _currentMonth.Month || currentDate.DayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
            {
                if (CalendarGrid.RowDefinitions.Count <= row)
                    CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                for (int col = 0; col < 7; col++)
                {
                    var dayBox = CreateDayCell(currentDate);
                    Grid.SetRow(dayBox, row);
                    Grid.SetColumn(dayBox, col);
                    CalendarGrid.Children.Add(dayBox);
                    currentDate = currentDate.AddDays(1);
                }
                row++;
            }
        }

        // Создаёт ячейку для одного дня (число месяца + количество звёзд числом)
        private Frame CreateDayCell(DateTime date)
        {
            bool isCurrentMonth = date.Month == _currentMonth.Month;
            int starCount = _stars.ContainsKey(date.Date) ? _stars[date.Date] : 0;

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

            // Количество звёзд (всегда число)
            var starsLabel = new Label
            {
                Text = starCount > 0 ? $"⭐ {starCount}" : "0",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Colors.Goldenrod
            };
            stack.Children.Add(starsLabel);

            var frame = new Frame
            {
                Content = stack,
                BorderColor = isCurrentMonth ? Colors.LightBlue : Colors.LightGray,
                CornerRadius = 5,
                Padding = 5,
                BackgroundColor = (date.Date == DateTime.Today) ? Colors.LightYellow : Colors.White
            };

            // Клик по ячейке — меняем звёзды через диалог
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) => await ChangeStarsViaDialog(date);
            frame.GestureRecognizers.Add(tapGesture);

            return frame;
        }

        // Обновляет содержимое ячеек без полной перестройки
        private void RefreshCalendar()
        {
            int childIndex = 7; // первые 7 — заголовки
            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int offset = (7 + (firstDayOfMonth.DayOfWeek - CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)) % 7;
            DateTime currentDate = firstDayOfMonth.AddDays(-offset);

            while (childIndex < CalendarGrid.Children.Count)
            {
                if (CalendarGrid.Children[childIndex] is Frame frame && frame.Content is VerticalStackLayout stack)
                {
                    int starCount = _stars.ContainsKey(currentDate.Date) ? _stars[currentDate.Date] : 0;
                    if (stack.Children.Count >= 2 && stack.Children[1] is Label starsLabel)
                    {
                        starsLabel.Text = starCount > 0 ? $"⭐ {starCount}" : "0";
                    }

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
            BuildCalendar();
        }

        private void OnNextMonthClicked(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateMonthLabel();
            BuildCalendar();
        }

        // ------------------ Кнопки для быстрого изменения звёзд за сегодня ------------------
        private void OnAddOneTodayClicked(object sender, EventArgs e)
        {
            AddOneStar(DateTime.Today);
            InfoLabel.Text = $"⭐ +1 к сегодня: стало {_stars.GetValueOrDefault(DateTime.Today.Date, 0)} звёзд";
        }

        private void OnSubtractOneTodayClicked(object sender, EventArgs e)
        {
            SubtractOneStar(DateTime.Today);
            InfoLabel.Text = $"⭐ -1 от сегодня: стало {_stars.GetValueOrDefault(DateTime.Today.Date, 0)} звёзд";
        }
    }
}
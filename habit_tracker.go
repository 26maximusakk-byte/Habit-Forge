// habit_tracker.go - Трекер привычек на Go (CLI)
package main

import (
	"bufio"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"
)

type Habit struct {
	ID          int      `json:"id"`
	Name        string   `json:"name"`
	Description string   `json:"description"`
	Frequency   string   `json:"frequency"`
	Created     string   `json:"created"`
	History     []string `json:"history"`
}

type Tracker struct {
	Habits []Habit `json:"habits"`
	NextID int     `json:"next_id"`
}

const dataFile = "habits.json"

func loadTracker() *Tracker {
	var t Tracker
	file, err := os.ReadFile(dataFile)
	if err != nil {
		t.Habits = []Habit{}
		t.NextID = 1
		return &t
	}
	err = json.Unmarshal(file, &t)
	if err != nil {
		t.Habits = []Habit{}
		t.NextID = 1
	}
	return &t
}

func saveTracker(t *Tracker) {
	data, _ := json.MarshalIndent(t, "", "  ")
	os.WriteFile(dataFile, data, 0644)
}

func addHabit(t *Tracker, name, description, frequency string) Habit {
	if frequency == "" {
		frequency = "daily"
	}
	h := Habit{
		ID:          t.NextID,
		Name:        name,
		Description: description,
		Frequency:   frequency,
		Created:     time.Now().Format("2006-01-02"),
		History:     []string{},
	}
	t.Habits = append(t.Habits, h)
	t.NextID++
	saveTracker(t)
	return h
}

func editHabit(t *Tracker, id int, name, description, frequency *string) bool {
	for i, h := range t.Habits {
		if h.ID == id {
			if name != nil {
				t.Habits[i].Name = *name
			}
			if description != nil {
				t.Habits[i].Description = *description
			}
			if frequency != nil {
				t.Habits[i].Frequency = *frequency
			}
			saveTracker(t)
			return true
		}
	}
	return false
}

func deleteHabit(t *Tracker, id int) bool {
	for i, h := range t.Habits {
		if h.ID == id {
			t.Habits = append(t.Habits[:i], t.Habits[i+1:]...)
			saveTracker(t)
			return true
		}
	}
	return false
}

func checkHabit(t *Tracker, id int, dateStr string) bool {
	if dateStr == "" {
		dateStr = time.Now().Format("2006-01-02")
	}
	for i, h := range t.Habits {
		if h.ID == id {
			for _, d := range h.History {
				if d == dateStr {
					return false
				}
			}
			t.Habits[i].History = append(t.Habits[i].History, dateStr)
			saveTracker(t)
			return true
		}
	}
	return false
}

func uncheckHabit(t *Tracker, id int, dateStr string) bool {
	if dateStr == "" {
		dateStr = time.Now().Format("2006-01-02")
	}
	for i, h := range t.Habits {
		if h.ID == id {
			for j, d := range h.History {
				if d == dateStr {
					t.Habits[i].History = append(h.History[:j], h.History[j+1:]...)
					saveTracker(t)
					return true
				}
			}
			return false
		}
	}
	return false
}

func getHabits(t *Tracker) []Habit {
	return t.Habits
}

func getHabitStats(h Habit) (total int, weekCount int, weekPercent float64, streak int, lastCheck string) {
	total = len(h.History)
	today := time.Now()
	weekAgo := today.AddDate(0, 0, -7)
	weekAgoStr := weekAgo.Format("2006-01-02")
	for _, d := range h.History {
		if d >= weekAgoStr {
			weekCount++
		}
	}
	expected := 7
	switch h.Frequency {
	case "weekly":
		expected = 1
	case "monthly":
		expected = 1
	}
	if expected > 0 {
		weekPercent = float64(weekCount) / float64(expected) * 100
	}
	// streak
	streak = 0
	checkDate := today
	for {
		ds := checkDate.Format("2006-01-02")
		found := false
		for _, d := range h.History {
			if d == ds {
				found = true
				break
			}
		}
		if found {
			streak++
			checkDate = checkDate.AddDate(0, 0, -1)
		} else {
			break
		}
	}
	if len(h.History) > 0 {
		lastCheck = h.History[len(h.History)-1]
	}
	return
}

func main() {
	var (
		cmd         string
		name        string
		description string
		frequency   string
		id          int
		dateStr     string
	)
	flag.StringVar(&cmd, "cmd", "", "Команда: add, list, check, uncheck, stats, edit, delete")
	flag.StringVar(&name, "name", "", "Название")
	flag.StringVar(&description, "description", "", "Описание")
	flag.StringVar(&frequency, "frequency", "", "daily/weekly/monthly")
	flag.IntVar(&id, "id", 0, "ID привычки")
	flag.StringVar(&dateStr, "date", "", "Дата (ГГГГ-ММ-ДД)")
	flag.Parse()

	tracker := loadTracker()

	switch cmd {
	case "add":
		if name == "" {
			fmt.Println("Укажите --name")
			return
		}
		h := addHabit(tracker, name, description, frequency)
		fmt.Printf("✅ Привычка #%d '%s' добавлена\n", h.ID, h.Name)
	case "list":
		habits := getHabits(tracker)
		if len(habits) == 0 {
			fmt.Println("Нет привычек.")
			return
		}
		todayStr := time.Now().Format("2006-01-02")
		fmt.Printf("%-4s %-20s %-10s %s\n", "ID", "Название", "Частота", "Сегодня")
		for _, h := range habits {
			done := "❌"
			for _, d := range h.History {
				if d == todayStr {
					done = "✅"
					break
				}
			}
			fmt.Printf("%-4d %-20s %-10s %s\n", h.ID, h.Name, h.Frequency, done)
		}
	case "check":
		if id == 0 {
			fmt.Println("Укажите --id")
			return
		}
		if checkHabit(tracker, id, dateStr) {
			fmt.Printf("✅ Привычка #%d отмечена выполнена\n", id)
		} else {
			fmt.Printf("❌ Не удалось отметить (возможно, уже отмечена или не найдена)\n")
		}
	case "uncheck":
		if id == 0 {
			fmt.Println("Укажите --id")
			return
		}
		if uncheckHabit(tracker, id, dateStr) {
			fmt.Printf("✅ Отметка снята для #%d\n", id)
		} else {
			fmt.Printf("❌ Не удалось снять отметку\n")
		}
	case "stats":
		if id == 0 {
			fmt.Println("Укажите --id")
			return
		}
		var h *Habit
		for _, habit := range tracker.Habits {
			if habit.ID == id {
				h = &habit
				break
			}
		}
		if h == nil {
			fmt.Println("Привычка не найдена")
			return
		}
		total, weekCount, weekPercent, streak, lastCheck := getHabitStats(*h)
		fmt.Printf("📊 Статистика для '%s':\n", h.Name)
		fmt.Printf("  Всего выполнений: %d\n", total)
		fmt.Printf("  За последние 7 дней: %d (%.1f%%)\n", weekCount, weekPercent)
		fmt.Printf("  Текущая серия (streak): %d дней\n", streak)
		fmt.Printf("  Последнее выполнение: %s\n", lastCheck)
	case "edit":
		if id == 0 {
			fmt.Println("Укажите --id")
			return
		}
		var namePtr *string
		var descPtr *string
		var freqPtr *string
		if name != "" {
			namePtr = &name
		}
		if description != "" {
			descPtr = &description
		}
		if frequency != "" {
			freqPtr = &frequency
		}
		if editHabit(tracker, id, namePtr, descPtr, freqPtr) {
			fmt.Printf("✅ Привычка #%d обновлена\n", id)
		} else {
			fmt.Printf("❌ Привычка не найдена\n")
		}
	case "delete":
		if id == 0 {
			fmt.Println("Укажите --id")
			return
		}
		if deleteHabit(tracker, id) {
			fmt.Printf("✅ Привычка #%d удалена\n", id)
		} else {
			fmt.Printf("❌ Привычка не найдена\n")
		}
	default:
		interactiveMode(tracker)
	}
}

func interactiveMode(t *Tracker) {
	scanner := bufio.NewScanner(os.Stdin)
	for {
		fmt.Println("\n🌱 Трекер привычек (интерактивный)")
		fmt.Println("1. Добавить привычку")
		fmt.Println("2. Список привычек")
		fmt.Println("3. Отметить выполнение")
		fmt.Println("4. Отменить выполнение")
		fmt.Println("5. Статистика")
		fmt.Println("6. Редактировать")
		fmt.Println("7. Удалить")
		fmt.Println("0. Выход")
		fmt.Print("Выберите действие: ")
		scanner.Scan()
		choice := scanner.Text()
		switch choice {
		case "0":
			return
		case "1":
			fmt.Print("Название: ")
			scanner.Scan()
			name := scanner.Text()
			if name == "" {
				fmt.Println("Название обязательно")
				continue
			}
			fmt.Print("Описание (необязательно): ")
			scanner.Scan()
			desc := scanner.Text()
			fmt.Print("Частота (daily/weekly/monthly, по умолчанию daily): ")
			scanner.Scan()
			freq := scanner.Text()
			if freq == "" {
				freq = "daily"
			}
			h := addHabit(t, name, desc, freq)
			fmt.Printf("✅ Привычка #%d '%s' добавлена\n", h.ID, h.Name)
		case "2":
			habits := getHabits(t)
			if len(habits) == 0 {
				fmt.Println("Нет привычек.")
				continue
			}
			todayStr := time.Now().Format("2006-01-02")
			fmt.Printf("%-4s %-20s %-10s %s\n", "ID", "Название", "Частота", "Сегодня")
			for _, h := range habits {
				done := "❌"
				for _, d := range h.History {
					if d == todayStr {
						done = "✅"
						break
					}
				}
				fmt.Printf("%-4d %-20s %-10s %s\n", h.ID, h.Name, h.Frequency, done)
			}
		case "3":
			fmt.Print("ID привычки: ")
			scanner.Scan()
			idStr := scanner.Text()
			id, err := strconv.Atoi(idStr)
			if err != nil {
				fmt.Println("Неверный ID")
				continue
			}
			if checkHabit(t, id, "") {
				fmt.Printf("✅ Привычка #%d отмечена выполнена\n", id)
			} else {
				fmt.Printf("❌ Не удалось отметить\n")
			}
		case "4":
			fmt.Print("ID привычки: ")
			scanner.Scan()
			idStr := scanner.Text()
			id, err := strconv.Atoi(idStr)
			if err != nil {
				fmt.Println("Неверный ID")
				continue
			}
			if uncheckHabit(t, id, "") {
				fmt.Printf("✅ Отметка снята для #%d\n", id)
			} else {
				fmt.Printf("❌ Не удалось снять отметку\n")
			}
		case "5":
			fmt.Print("ID привычки: ")
			scanner.Scan()
			idStr := scanner.Text()
			id, err := strconv.Atoi(idStr)
			if err != nil {
				fmt.Println("Неверный ID")
				continue
			}
			var h *Habit
			for _, habit := range t.Habits {
				if habit.ID == id {
					h = &habit
					break
				}
			}
			if h == nil {
				fmt.Println("Привычка не найдена")
				continue
			}
			total, weekCount, weekPercent, streak, lastCheck := getHabitStats(*h)
			fmt.Printf("📊 Статистика для '%s':\n", h.Name)
			fmt.Printf("  Всего выполнений: %d\n", total)
			fmt.Printf("  За последние 7 дней: %d (%.1f%%)\n", weekCount, weekPercent)
			fmt.Printf("  Текущая серия (streak): %d дней\n", streak)
			fmt.Printf("  Последнее выполнение: %s\n", lastCheck)
		case "6":
			fmt.Print("ID привычки: ")
			scanner.Scan()
			idStr := scanner.Text()
			id, err := strconv.Atoi(idStr)
			if err != nil {
				fmt.Println("Неверный ID")
				continue
			}
			var habit *Habit
			for i := range t.Habits {
				if t.Habits[i].ID == id {
					habit = &t.Habits[i]
					break
				}
			}
			if habit == nil {
				fmt.Println("Привычка не найдена")
				continue
			}
			fmt.Println("Оставьте пустым, чтобы не менять.")
			fmt.Printf("Название (%s): ", habit.Name)
			scanner.Scan()
			newName := scanner.Text()
			fmt.Printf("Описание (%s): ", habit.Description)
			scanner.Scan()
			newDesc := scanner.Text()
			fmt.Printf("Частота (%s): ", habit.Frequency)
			scanner.Scan()
			newFreq := scanner.Text()
			var namePtr *string
			var descPtr *string
			var freqPtr *string
			if newName != "" {
				namePtr = &newName
			}
			if newDesc != "" {
				descPtr = &newDesc
			}
			if newFreq != "" {
				freqPtr = &newFreq
			}
			if editHabit(t, id, namePtr, descPtr, freqPtr) {
				fmt.Println("✅ Обновлено")
			} else {
				fmt.Println("❌ Ошибка")
			}
		case "7":
			fmt.Print("ID для удаления: ")
			scanner.Scan()
			idStr := scanner.Text()
			id, err := strconv.Atoi(idStr)
			if err != nil {
				fmt.Println("Неверный ID")
				continue
			}
			if deleteHabit(t, id) {
				fmt.Println("✅ Удалено")
			} else {
				fmt.Println("❌ Не найдено")
			}
		default:
			fmt.Println("Неверный выбор")
		}
	}
}

// habit_tracker.rs - Трекер привычек на Rust (CLI)
use serde::{Serialize, Deserialize};
use std::collections::HashMap;
use std::fs;
use std::io::{self, Write, BufRead};
use std::path::Path;
use std::str::FromStr;
use chrono::{DateTime, Local, Duration};

#[derive(Serialize, Deserialize, Clone)]
struct Habit {
    id: u32,
    name: String,
    description: String,
    frequency: String,
    created: String,
    history: Vec<String>,
}

#[derive(Serialize, Deserialize)]
struct Tracker {
    habits: Vec<Habit>,
    next_id: u32,
}

impl Tracker {
    fn load() -> Self {
        let path = "habits.json";
        if Path::new(path).exists() {
            if let Ok(data) = fs::read_to_string(path) {
                if let Ok(t) = serde_json::from_str(&data) {
                    return t;
                }
            }
        }
        Tracker { habits: vec![], next_id: 1 }
    }

    fn save(&self) {
        let data = serde_json::to_string_pretty(self).unwrap();
        fs::write("habits.json", data).unwrap();
    }
}

fn add_habit(tracker: &mut Tracker, name: &str, description: &str, frequency: &str) -> Habit {
    let freq = if frequency.is_empty() { "daily" } else { frequency };
    let h = Habit {
        id: tracker.next_id,
        name: name.to_string(),
        description: description.to_string(),
        frequency: freq.to_string(),
        created: Local::now().format("%Y-%m-%d").to_string(),
        history: vec![],
    };
    tracker.habits.push(h.clone());
    tracker.next_id += 1;
    tracker.save();
    h
}

fn edit_habit(tracker: &mut Tracker, id: u32, name: Option<&str>, description: Option<&str>, frequency: Option<&str>) -> bool {
    for h in &mut tracker.habits {
        if h.id == id {
            if let Some(n) = name { h.name = n.to_string(); }
            if let Some(d) = description { h.description = d.to_string(); }
            if let Some(f) = frequency { h.frequency = f.to_string(); }
            tracker.save();
            return true;
        }
    }
    false
}

fn delete_habit(tracker: &mut Tracker, id: u32) -> bool {
    let len = tracker.habits.len();
    tracker.habits.retain(|h| h.id != id);
    if tracker.habits.len() < len {
        tracker.save();
        return true;
    }
    false
}

fn check_habit(tracker: &mut Tracker, id: u32, date_str: Option<&str>) -> bool {
    let date = if let Some(d) = date_str { d.to_string() } else { Local::now().format("%Y-%m-%d").to_string() };
    for h in &mut tracker.habits {
        if h.id == id {
            if h.history.contains(&date) {
                return false;
            }
            h.history.push(date);
            tracker.save();
            return true;
        }
    }
    false
}

fn uncheck_habit(tracker: &mut Tracker, id: u32, date_str: Option<&str>) -> bool {
    let date = if let Some(d) = date_str { d.to_string() } else { Local::now().format("%Y-%m-%d").to_string() };
    for h in &mut tracker.habits {
        if h.id == id {
            if let Some(pos) = h.history.iter().position(|d| d == &date) {
                h.history.remove(pos);
                tracker.save();
                return true;
            }
            return false;
        }
    }
    false
}

fn get_habits(tracker: &Tracker) -> &Vec<Habit> {
    &tracker.habits
}

fn get_habit_stats(habit: &Habit) -> (u32, u32, f64, u32, Option<String>) {
    let total = habit.history.len() as u32;
    let today = Local::now();
    let week_ago = today - Duration::days(7);
    let week_ago_str = week_ago.format("%Y-%m-%d").to_string();
    let week_count = habit.history.iter().filter(|d| *d >= &week_ago_str).count() as u32;
    let expected = match habit.frequency.as_str() {
        "daily" => 7,
        "weekly" => 1,
        "monthly" => 1,
        _ => 7,
    };
    let week_percent = if expected > 0 { (week_count as f64 / expected as f64) * 100.0 } else { 0.0 };
    // streak
    let mut streak = 0;
    let mut check_date = today;
    loop {
        let ds = check_date.format("%Y-%m-%d").to_string();
        if habit.history.contains(&ds) {
            streak += 1;
            check_date = check_date - Duration::days(1);
        } else {
            break;
        }
    }
    let last_check = if habit.history.is_empty() { None } else { Some(habit.history.last().unwrap().clone()) };
    (total, week_count, week_percent, streak, last_check)
}

fn read_line(prompt: &str) -> String {
    print!("{}", prompt);
    io::stdout().flush().unwrap();
    let mut input = String::new();
    io::stdin().read_line(&mut input).unwrap();
    input.trim().to_string()
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    if args.len() < 2 {
        interactive_mode();
        return;
    }
    let mut tracker = Tracker::load();
    match args[1].as_str() {
        "add" => {
            let mut name = String::new();
            let mut description = String::new();
            let mut frequency = String::new();
            let mut i = 2;
            while i < args.len() {
                match args[i].as_str() {
                    "--name" => { name = args[i+1].clone(); i += 2; }
                    "--description" => { description = args[i+1].clone(); i += 2; }
                    "--frequency" => { frequency = args[i+1].clone(); i += 2; }
                    _ => { i += 1; }
                }
            }
            if name.is_empty() {
                println!("Укажите --name");
                return;
            }
            let h = add_habit(&mut tracker, &name, &description, &frequency);
            println!("✅ Привычка #{} '{}' добавлена", h.id, h.name);
        }
        "list" => {
            let habits = get_habits(&tracker);
            if habits.is_empty() {
                println!("Нет привычек.");
                return;
            }
            let today_str = Local::now().format("%Y-%m-%d").to_string();
            println!("{:<4} {:<20} {:<10} {}", "ID", "Название", "Частота", "Сегодня");
            for h in habits {
                let done = if h.history.contains(&today_str) { "✅" } else { "❌" };
                println!("{:<4} {:<20} {:<10} {}", h.id, h.name, h.frequency, done);
            }
        }
        "check" => {
            let mut id = 0;
            let mut date = None;
            let mut i = 2;
            while i < args.len() {
                match args[i].as_str() {
                    "--id" => { id = args[i+1].parse().unwrap_or(0); i += 2; }
                    "--date" => { date = Some(args[i+1].clone()); i += 2; }
                    _ => { i += 1; }
                }
            }
            if id == 0 {
                println!("Укажите --id");
                return;
            }
            if check_habit(&mut tracker, id, date.as_deref()) {
                println!("✅ Привычка #{} отмечена выполнена", id);
            } else {
                println!("❌ Не удалось отметить");
            }
        }
        "uncheck" => {
            let mut id = 0;
            let mut date = None;
            let mut i = 2;
            while i < args.len() {
                match args[i].as_str() {
                    "--id" => { id = args[i+1].parse().unwrap_or(0); i += 2; }
                    "--date" => { date = Some(args[i+1].clone()); i += 2; }
                    _ => { i += 1; }
                }
            }
            if id == 0 {
                println!("Укажите --id");
                return;
            }
            if uncheck_habit(&mut tracker, id, date.as_deref()) {
                println!("✅ Отметка снята для #{}", id);
            } else {
                println!("❌ Не удалось снять отметку");
            }
        }
        "stats" => {
            let mut id = 0;
            let mut i = 2;
            while i < args.len() {
                if args[i] == "--id" {
                    id = args[i+1].parse().unwrap_or(0);
                    break;
                }
                i += 1;
            }
            if id == 0 {
                println!("Укажите --id");
                return;
            }
            let habit = tracker.habits.iter().find(|h| h.id == id);
            if let Some(h) = habit {
                let (total, week_count, week_percent, streak, last_check) = get_habit_stats(h);
                println!("📊 Статистика для '{}':", h.name);
                println!("  Всего выполнений: {}", total);
                println!("  За последние 7 дней: {} ({:.1}%)", week_count, week_percent);
                println!("  Текущая серия (streak): {} дней", streak);
                println!("  Последнее выполнение: {}", last_check.unwrap_or_else(|| "никогда".to_string()));
            } else {
                println!("Привычка не найдена");
            }
        }
        "edit" => {
            let mut id = 0;
            let mut name = None;
            let mut description = None;
            let mut frequency = None;
            let mut i = 2;
            while i < args.len() {
                match args[i].as_str() {
                    "--id" => { id = args[i+1].parse().unwrap_or(0); i += 2; }
                    "--name" => { name = Some(args[i+1].clone()); i += 2; }
                    "--description" => { description = Some(args[i+1].clone()); i += 2; }
                    "--frequency" => { frequency = Some(args[i+1].clone()); i += 2; }
                    _ => { i += 1; }
                }
            }
            if id == 0 {
                println!("Укажите --id");
                return;
            }
            if edit_habit(&mut tracker, id, name.as_deref(), description.as_deref(), frequency.as_deref()) {
                println!("✅ Привычка #{} обновлена", id);
            } else {
                println!("❌ Привычка не найдена");
            }
        }
        "delete" => {
            let mut id = 0;
            let mut i = 2;
            while i < args.len() {
                if args[i] == "--id" {
                    id = args[i+1].parse().unwrap_or(0);
                    break;
                }
                i += 1;
            }
            if id == 0 {
                println!("Укажите --id");
                return;
            }
            if delete_habit(&mut tracker, id) {
                println!("✅ Привычка #{} удалена", id);
            } else {
                println!("❌ Привычка не найдена");
            }
        }
        _ => interactive_mode(),
    }
}

fn interactive_mode() {
    let mut tracker = Tracker::load();
    let stdin = io::stdin();
    let mut stdout = io::stdout();
    loop {
        println!("\n🌱 Трекер привычек (интерактивный)");
        println!("1. Добавить привычку");
        println!("2. Список привычек");
        println!("3. Отметить выполнение");
        println!("4. Отменить выполнение");
        println!("5. Статистика");
        println!("6. Редактировать");
        println!("7. Удалить");
        println!("0. Выход");
        print!("Выберите действие: ");
        stdout.flush().unwrap();
        let mut choice = String::new();
        stdin.read_line(&mut choice).unwrap();
        match choice.trim() {
            "0" => break,
            "1" => {
                print!("Название: ");
                stdout.flush().unwrap();
                let mut name = String::new();
                stdin.read_line(&mut name).unwrap();
                let name = name.trim();
                if name.is_empty() {
                    println!("Название обязательно");
                    continue;
                }
                print!("Описание (необязательно): ");
                stdout.flush().unwrap();
                let mut desc = String::new();
                stdin.read_line(&mut desc).unwrap();
                print!("Частота (daily/weekly/monthly, по умолчанию daily): ");
                stdout.flush().unwrap();
                let mut freq = String::new();
                stdin.read_line(&mut freq).unwrap();
                let freq = freq.trim();
                let h = add_habit(&mut tracker, name, desc.trim(), freq);
                println!("✅ Привычка #{} '{}' добавлена", h.id, h.name);
            }
            "2" => {
                let habits = get_habits(&tracker);
                if habits.is_empty() {
                    println!("Нет привычек.");
                    continue;
                }
                let today_str = Local::now().format("%Y-%m-%d").to_string();
                println!("{:<4} {:<20} {:<10} {}", "ID", "Название", "Частота", "Сегодня");
                for h in habits {
                    let done = if h.history.contains(&today_str) { "✅" } else { "❌" };
                    println!("{:<4} {:<20} {:<10} {}", h.id, h.name, h.frequency, done);
                }
            }
            "3" => {
                print!("ID привычки: ");
                stdout.flush().unwrap();
                let mut id_str = String::new();
                stdin.read_line(&mut id_str).unwrap();
                let id = id_str.trim().parse::<u32>().unwrap_or(0);
                if id == 0 {
                    println!("Неверный ID");
                    continue;
                }
                if check_habit(&mut tracker, id, None) {
                    println!("✅ Привычка #{} отмечена выполнена", id);
                } else {
                    println!("❌ Не удалось отметить");
                }
            }
            "4" => {
                print!("ID привычки: ");
                stdout.flush().unwrap();
                let mut id_str = String::new();
                stdin.read_line(&mut id_str).unwrap();
                let id = id_str.trim().parse::<u32>().unwrap_or(0);
                if id == 0 {
                    println!("Неверный ID");
                    continue;
                }
                if uncheck_habit(&mut tracker, id, None) {
                    println!("✅ Отметка снята для #{}", id);
                } else {
                    println!("❌ Не удалось снять отметку");
                }
            }
            "5" => {
                print!("ID привычки: ");
                stdout.flush().unwrap();
                let mut id_str = String::new();
                stdin.read_line(&mut id_str).unwrap();
                let id = id_str.trim().parse::<u32>().unwrap_or(0);
                if id == 0 {
                    println!("Неверный ID");
                    continue;
                }
                let habit = tracker.habits.iter().find(|h| h.id == id);
                if let Some(h) = habit {
                    let (total, week_count, week_percent, streak, last_check) = get_habit_stats(h);
                    println!("📊 Статистика для '{}':", h.name);
                    println!("  Всего выполнений: {}", total);
                    println!("  За последние 7 дней: {} ({:.1}%)", week_count, week_percent);
                    println!("  Текущая серия (streak): {} дней", streak);
                    println!("  Последнее выполнение: {}", last_check.unwrap_or_else(|| "никогда".to_string()));
                } else {
                    println!("Привычка не найдена");
                }
            }
            "6" => {
                print!("ID привычки: ");
                stdout.flush().unwrap();
                let mut id_str = String::new();
                stdin.read_line(&mut id_str).unwrap();
                let id = id_str.trim().parse::<u32>().unwrap_or(0);
                if id == 0 {
                    println!("Неверный ID");
                    continue;
                }
                let habit_idx = tracker.habits.iter().position(|h| h.id == id);
                if let Some(idx) = habit_idx {
                    let habit = &tracker.habits[idx];
                    println!("Оставьте пустым, чтобы не менять.");
                    print!("Название ({}): ", habit.name);
                    stdout.flush().unwrap();
                    let mut new_name = String::new();
                    stdin.read_line(&mut new_name).unwrap();
                    let new_name = if new_name.trim().is_empty() { None } else { Some(new_name.trim()) };
                    print!("Описание ({}): ", habit.description);
                    stdout.flush().unwrap();
                    let mut new_desc = String::new();
                    stdin.read_line(&mut new_desc).unwrap();
                    let new_desc = if new_desc.trim().is_empty() { None } else { Some(new_desc.trim()) };
                    print!("Частота ({}): ", habit.frequency);
                    stdout.flush().unwrap();
                    let mut new_freq = String::new();
                    stdin.read_line(&mut new_freq).unwrap();
                    let new_freq = if new_freq.trim().is_empty() { None } else { Some(new_freq.trim()) };
                    if edit_habit(&mut tracker, id, new_name, new_desc, new_freq) {
                        println!("✅ Обновлено");
                    } else {
                        println!("❌ Ошибка");
                    }
                } else {
                    println!("Привычка не найдена");
                }
            }
            "7" => {
                print!("ID для удаления: ");
                stdout.flush().unwrap();
                let mut id_str = String::new();
                stdin.read_line(&mut id_str).unwrap();
                let id = id_str.trim().parse::<u32>().unwrap_or(0);
                if id == 0 {
                    println!("Неверный ID");
                    continue;
                }
                if delete_habit(&mut tracker, id) {
                    println!("✅ Удалено");
                } else {
                    println!("❌ Не найдено");
                }
            }
            _ => println!("Неверный выбор"),
        }
    }
}

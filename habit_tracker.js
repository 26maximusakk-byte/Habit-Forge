#!/usr/bin/env node
/**
 * habit_tracker.js - Трекер привычек на JavaScript (Node.js CLI)
 */
const fs = require('fs');
const path = require('path');
const { program } = require('commander');
const { v4: uuidv4 } = require('uuid');

const DATA_FILE = path.join(__dirname, 'habits.json');

class Habit {
    constructor(name, description, frequency) {
        this.id = uuidv4();
        this.name = name;
        this.description = description || '';
        this.frequency = frequency || 'daily';
        this.created = new Date().toISOString().slice(0,10);
        this.history = [];
    }
}

class HabitTracker {
    constructor() {
        this.habits = [];
        this.load();
    }

    load() {
        if (fs.existsSync(DATA_FILE)) {
            try {
                const data = JSON.parse(fs.readFileSync(DATA_FILE, 'utf8'));
                this.habits = data;
            } catch {}
        }
    }

    save() {
        fs.writeFileSync(DATA_FILE, JSON.stringify(this.habits, null, 2));
    }

    addHabit(name, description, frequency) {
        const habit = new Habit(name, description, frequency);
        this.habits.push(habit);
        this.save();
        return habit;
    }

    editHabit(id, name, description, frequency) {
        const habit = this.habits.find(h => h.id === id);
        if (!habit) return false;
        if (name !== undefined) habit.name = name;
        if (description !== undefined) habit.description = description;
        if (frequency !== undefined) habit.frequency = frequency;
        this.save();
        return true;
    }

    deleteHabit(id) {
        const idx = this.habits.findIndex(h => h.id === id);
        if (idx === -1) return false;
        this.habits.splice(idx, 1);
        this.save();
        return true;
    }

    checkHabit(id, dateStr) {
        const habit = this.habits.find(h => h.id === id);
        if (!habit) return false;
        if (!dateStr) dateStr = new Date().toISOString().slice(0,10);
        if (habit.history.includes(dateStr)) return false;
        habit.history.push(dateStr);
        this.save();
        return true;
    }

    uncheckHabit(id, dateStr) {
        const habit = this.habits.find(h => h.id === id);
        if (!habit) return false;
        if (!dateStr) dateStr = new Date().toISOString().slice(0,10);
        const idx = habit.history.indexOf(dateStr);
        if (idx === -1) return false;
        habit.history.splice(idx, 1);
        this.save();
        return true;
    }

    getHabits() {
        return this.habits;
    }

    getHabitStats(habit) {
        const today = new Date();
        const todayStr = today.toISOString().slice(0,10);
        const weekAgo = new Date();
        weekAgo.setDate(weekAgo.getDate() - 7);
        const weekAgoStr = weekAgo.toISOString().slice(0,10);
        const total = habit.history.length;
        const lastWeek = habit.history.filter(d => d >= weekAgoStr);
        const weekCount = lastWeek.length;
        let expected = 7;
        if (habit.frequency === 'weekly') expected = 1;
        else if (habit.frequency === 'monthly') expected = 1;
        const weekPercent = expected > 0 ? (weekCount / expected) * 100 : 0;
        // Streak
        let streak = 0;
        let checkDate = new Date(today);
        while (true) {
            const ds = checkDate.toISOString().slice(0,10);
            if (habit.history.includes(ds)) {
                streak++;
                checkDate.setDate(checkDate.getDate() - 1);
            } else {
                break;
            }
        }
        const lastCheck = habit.history.length ? habit.history[habit.history.length-1] : null;
        return { total, weekCount, weekPercent, streak, lastCheck };
    }
}

program
    .command('add <name>')
    .option('-d, --description <desc>', 'Описание')
    .option('-f, --frequency <freq>', 'Частота (daily/weekly/monthly)', 'daily')
    .action((name, options) => {
        const tracker = new HabitTracker();
        const h = tracker.addHabit(name, options.description, options.frequency);
        console.log(`✅ Привычка ${h.id} '${h.name}' добавлена`);
    });

program
    .command('list')
    .action(() => {
        const tracker = new HabitTracker();
        const habits = tracker.getHabits();
        if (!habits.length) {
            console.log('Нет привычек.');
            return;
        }
        const todayStr = new Date().toISOString().slice(0,10);
        console.log('ID'.padEnd(36) + 'Название'.padEnd(20) + 'Частота'.padEnd(10) + 'Сегодня');
        habits.forEach(h => {
            const done = h.history.includes(todayStr) ? '✅' : '❌';
            console.log(`${h.id.padEnd(36)} ${h.name.slice(0,20).padEnd(20)} ${h.frequency.padEnd(10)} ${done}`);
        });
    });

program
    .command('check <id>')
    .option('--date <date>', 'Дата (ГГГГ-ММ-ДД)')
    .action((id, options) => {
        const tracker = new HabitTracker();
        if (tracker.checkHabit(id, options.date)) {
            console.log(`✅ Привычка ${id} отмечена выполнена`);
        } else {
            console.log(`❌ Не удалось отметить (возможно, уже отмечена или не найдена)`);
        }
    });

program
    .command('uncheck <id>')
    .option('--date <date>', 'Дата (ГГГГ-ММ-ДД)')
    .action((id, options) => {
        const tracker = new HabitTracker();
        if (tracker.uncheckHabit(id, options.date)) {
            console.log(`✅ Отметка снята для ${id}`);
        } else {
            console.log(`❌ Не удалось снять отметку`);
        }
    });

program
    .command('stats <id>')
    .action((id) => {
        const tracker = new HabitTracker();
        const habits = tracker.getHabits();
        const h = habits.find(h => h.id === id);
        if (!h) {
            console.log('Привычка не найдена');
            return;
        }
        const stats = tracker.getHabitStats(h);
        console.log(`📊 Статистика для '${h.name}':`);
        console.log(`  Всего выполнений: ${stats.total}`);
        console.log(`  За последние 7 дней: ${stats.weekCount} (${stats.weekPercent.toFixed(1)}%)`);
        console.log(`  Текущая серия (streak): ${stats.streak} дней`);
        console.log(`  Последнее выполнение: ${stats.lastCheck || 'никогда'}`);
    });

program
    .command('edit <id>')
    .option('--name <name>', 'Новое название')
    .option('--description <desc>', 'Новое описание')
    .option('--frequency <freq>', 'Новая частота')
    .action((id, options) => {
        const tracker = new HabitTracker();
        if (tracker.editHabit(id, options.name, options.description, options.frequency)) {
            console.log(`✅ Привычка ${id} обновлена`);
        } else {
            console.log(`❌ Привычка не найдена`);
        }
    });

program
    .command('delete <id>')
    .action((id) => {
        const tracker = new HabitTracker();
        if (tracker.deleteHabit(id)) {
            console.log(`✅ Привычка ${id} удалена`);
        } else {
            console.log(`❌ Привычка не найдена`);
        }
    });

program
    .command('interactive')
    .description('Интерактивный режим')
    .action(() => {
        const tracker = new HabitTracker();
        const readline = require('readline');
        const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
        const prompt = (q) => new Promise(resolve => rl.question(q, resolve));

        (async () => {
            while (true) {
                console.log('\n🌱 Трекер привычек (интерактивный)');
                console.log('1. Добавить привычку');
                console.log('2. Список привычек');
                console.log('3. Отметить выполнение');
                console.log('4. Отменить выполнение');
                console.log('5. Статистика');
                console.log('6. Редактировать');
                console.log('7. Удалить');
                console.log('0. Выход');
                const choice = await prompt('Выберите действие: ');
                switch (choice.trim()) {
                    case '0': rl.close(); return;
                    case '1': {
                        const name = await prompt('Название: ');
                        if (!name) { console.log('Название обязательно'); break; }
                        const desc = await prompt('Описание (необязательно): ');
                        const freq = await prompt('Частота (daily/weekly/monthly, по умолчанию daily): ');
                        const h = tracker.addHabit(name, desc, freq || 'daily');
                        console.log(`✅ Привычка ${h.id} '${h.name}' добавлена`);
                        break;
                    }
                    case '2': {
                        const habits = tracker.getHabits();
                        if (!habits.length) { console.log('Нет привычек.'); break; }
                        const todayStr = new Date().toISOString().slice(0,10);
                        console.log('ID'.padEnd(36) + 'Название'.padEnd(20) + 'Частота'.padEnd(10) + 'Сегодня');
                        habits.forEach(h => {
                            const done = h.history.includes(todayStr) ? '✅' : '❌';
                            console.log(`${h.id.padEnd(36)} ${h.name.slice(0,20).padEnd(20)} ${h.frequency.padEnd(10)} ${done}`);
                        });
                        break;
                    }
                    case '3': {
                        const id = await prompt('ID привычки: ');
                        if (tracker.checkHabit(id)) {
                            console.log(`✅ Привычка ${id} отмечена выполнена`);
                        } else {
                            console.log(`❌ Не удалось отметить`);
                        }
                        break;
                    }
                    case '4': {
                        const id = await prompt('ID привычки: ');
                        if (tracker.uncheckHabit(id)) {
                            console.log(`✅ Отметка снята для ${id}`);
                        } else {
                            console.log(`❌ Не удалось снять отметку`);
                        }
                        break;
                    }
                    case '5': {
                        const id = await prompt('ID привычки: ');
                        const habits = tracker.getHabits();
                        const h = habits.find(h => h.id === id);
                        if (!h) { console.log('Привычка не найдена'); break; }
                        const stats = tracker.getHabitStats(h);
                        console.log(`📊 Статистика для '${h.name}':`);
                        console.log(`  Всего выполнений: ${stats.total}`);
                        console.log(`  За последние 7 дней: ${stats.weekCount} (${stats.weekPercent.toFixed(1)}%)`);
                        console.log(`  Текущая серия (streak): ${stats.streak} дней`);
                        console.log(`  Последнее выполнение: ${stats.lastCheck || 'никогда'}`);
                        break;
                    }
                    case '6': {
                        const id = await prompt('ID привычки: ');
                        const habits = tracker.getHabits();
                        const h = habits.find(h => h.id === id);
                        if (!h) { console.log('Привычка не найдена'); break; }
                        console.log('Оставьте пустым, чтобы не менять.');
                        const newName = await prompt(`Название (${h.name}): `);
                        const newDesc = await prompt(`Описание (${h.description}): `);
                        const newFreq = await prompt(`Частота (${h.frequency}): `);
                        if (tracker.editHabit(id, newName || undefined, newDesc || undefined, newFreq || undefined)) {
                            console.log('✅ Обновлено');
                        } else {
                            console.log('❌ Ошибка');
                        }
                        break;
                    }
                    case '7': {
                        const id = await prompt('ID для удаления: ');
                        if (tracker.deleteHabit(id)) {
                            console.log('✅ Удалено');
                        } else {
                            console.log('❌ Не найдено');
                        }
                        break;
                    }
                    default: console.log('Неверный выбор');
                }
            }
        })();
    });

if (process.argv.length <= 2) {
    process.argv.push('interactive');
}
program.parse(process.argv);

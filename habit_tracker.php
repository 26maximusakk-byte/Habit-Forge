<?php
// habit_tracker.php - Трекер привычек на PHP (CLI + веб)
// CLI: php habit_tracker.php add --name="Утренняя зарядка" --frequency=daily

$dataFile = 'habits.json';

function loadData() {
    global $dataFile;
    if (!file_exists($dataFile)) {
        return ['habits' => [], 'next_id' => 1];
    }
    $json = file_get_contents($dataFile);
    $data = json_decode($json, true);
    if (!$data) $data = ['habits' => [], 'next_id' => 1];
    return $data;
}

function saveData($data) {
    global $dataFile;
    file_put_contents($dataFile, json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

function addHabit(&$data, $name, $description, $frequency) {
    if (!$frequency) $frequency = 'daily';
    $id = $data['next_id']++;
    $habit = [
        'id' => $id,
        'name' => $name,
        'description' => $description,
        'frequency' => $frequency,
        'created' => date('Y-m-d'),
        'history' => []
    ];
    $data['habits'][] = $habit;
    saveData($data);
    return $habit;
}

function editHabit(&$data, $id, $name, $description, $frequency) {
    foreach ($data['habits'] as &$h) {
        if ($h['id'] == $id) {
            if ($name !== null) $h['name'] = $name;
            if ($description !== null) $h['description'] = $description;
            if ($frequency !== null) $h['frequency'] = $frequency;
            saveData($data);
            return true;
        }
    }
    return false;
}

function deleteHabit(&$data, $id) {
    $filtered = array_filter($data['habits'], function($h) use ($id) { return $h['id'] != $id; });
    if (count($filtered) < count($data['habits'])) {
        $data['habits'] = array_values($filtered);
        saveData($data);
        return true;
    }
    return false;
}

function checkHabit(&$data, $id, $date = null) {
    if (!$date) $date = date('Y-m-d');
    foreach ($data['habits'] as &$h) {
        if ($h['id'] == $id) {
            if (in_array($date, $h['history'])) return false;
            $h['history'][] = $date;
            saveData($data);
            return true;
        }
    }
    return false;
}

function uncheckHabit(&$data, $id, $date = null) {
    if (!$date) $date = date('Y-m-d');
    foreach ($data['habits'] as &$h) {
        if ($h['id'] == $id) {
            $pos = array_search($date, $h['history']);
            if ($pos === false) return false;
            array_splice($h['history'], $pos, 1);
            saveData($data);
            return true;
        }
    }
    return false;
}

function getHabitStats($habit) {
    $total = count($habit['history']);
    $today = new DateTime();
    $weekAgo = (new DateTime())->modify('-7 days');
    $weekAgoStr = $weekAgo->format('Y-m-d');
    $weekCount = 0;
    foreach ($habit['history'] as $d) {
        if ($d >= $weekAgoStr) $weekCount++;
    }
    $expected = $habit['frequency'] == 'daily' ? 7 : ($habit['frequency'] == 'weekly' ? 1 : 1);
    $weekPercent = $expected > 0 ? ($weekCount / $expected * 100) : 0;
    // streak
    $streak = 0;
    $check = new DateTime();
    while (true) {
        $ds = $check->format('Y-m-d');
        if (in_array($ds, $habit['history'])) {
            $streak++;
            $check->modify('-1 day');
        } else {
            break;
        }
    }
    $lastCheck = count($habit['history']) ? $habit['history'][count($habit['history'])-1] : null;
    return ['total' => $total, 'weekCount' => $weekCount, 'weekPercent' => $weekPercent, 'streak' => $streak, 'lastCheck' => $lastCheck];
}

// ========== CLI ==========
if (php_sapi_name() === 'cli') {
    $options = getopt("", ["cmd:", "name:", "description:", "frequency:", "id:", "date:"]);
    $cmd = $options['cmd'] ?? null;
    $data = loadData();
    switch ($cmd) {
        case 'add':
            $name = $options['name'] ?? '';
            if (!$name) { echo "Укажите --name\n"; break; }
            $desc = $options['description'] ?? '';
            $freq = $options['frequency'] ?? 'daily';
            $h = addHabit($data, $name, $desc, $freq);
            echo "✅ Привычка #{$h['id']} '{$h['name']}' добавлена\n";
            break;
        case 'list':
            if (empty($data['habits'])) {
                echo "Нет привычек.\n";
            } else {
                $today = date('Y-m-d');
                printf("%-4s %-20s %-10s %s\n", "ID", "Название", "Частота", "Сегодня");
                foreach ($data['habits'] as $h) {
                    $done = in_array($today, $h['history']) ? "✅" : "❌";
                    printf("%-4d %-20s %-10s %s\n", $h['id'], $h['name'], $h['frequency'], $done);
                }
            }
            break;
        case 'check':
            $id = $options['id'] ?? 0;
            $date = $options['date'] ?? null;
            if (!$id) { echo "Укажите --id\n"; break; }
            if (checkHabit($data, $id, $date)) {
                echo "✅ Привычка #$id отмечена выполнена\n";
            } else {
                echo "❌ Не удалось отметить\n";
            }
            break;
        case 'uncheck':
            $id = $options['id'] ?? 0;
            $date = $options['date'] ?? null;
            if (!$id) { echo "Укажите --id\n"; break; }
            if (uncheckHabit($data, $id, $date)) {
                echo "✅ Отметка снята для #$id\n";
            } else {
                echo "❌ Не удалось снять отметку\n";
            }
            break;
        case 'stats':
            $id = $options['id'] ?? 0;
            if (!$id) { echo "Укажите --id\n"; break; }
            $habit = null;
            foreach ($data['habits'] as $h) {
                if ($h['id'] == $id) { $habit = $h; break; }
            }
            if (!$habit) { echo "Привычка не найдена\n"; break; }
            $stats = getHabitStats($habit);
            echo "📊 Статистика для '{$habit['name']}':\n";
            echo "  Всего выполнений: {$stats['total']}\n";
            printf("  За последние 7 дней: %d (%.1f%%)\n", $stats['weekCount'], $stats['weekPercent']);
            echo "  Текущая серия (streak): {$stats['streak']} дней\n";
            echo "  Последнее выполнение: " . ($stats['lastCheck'] ?? 'никогда') . "\n";
            break;
        case 'edit':
            $id = $options['id'] ?? 0;
            $name = $options['name'] ?? null;
            $desc = $options['description'] ?? null;
            $freq = $options['frequency'] ?? null;
            if (!$id) { echo "Укажите --id\n"; break; }
            if (editHabit($data, $id, $name, $desc, $freq)) {
                echo "✅ Привычка #$id обновлена\n";
            } else {
                echo "❌ Привычка не найдена\n";
            }
            break;
        case 'delete':
            $id = $options['id'] ?? 0;
            if (!$id) { echo "Укажите --id\n"; break; }
            if (deleteHabit($data, $id)) {
                echo "✅ Привычка #$id удалена\n";
            } else {
                echo "❌ Привычка не найдена\n";
            }
            break;
        default:
            interactiveMode($data);
            break;
    }
    exit;
}

// ========== ИНТЕРАКТИВНЫЙ РЕЖИМ ==========
function interactiveMode(&$data) {
    while (true) {
        echo "\n🌱 Трекер привычек (интерактивный)\n";
        echo "1. Добавить привычку\n";
        echo "2. Список привычек\n";
        echo "3. Отметить выполнение\n";
        echo "4. Отменить выполнение\n";
        echo "5. Статистика\n";
        echo "6. Редактировать\n";
        echo "7. Удалить\n";
        echo "0. Выход\n";
        echo "Выберите действие: ";
        $choice = trim(fgets(STDIN));
        switch ($choice) {
            case '0': return;
            case '1':
                echo "Название: ";
                $name = trim(fgets(STDIN));
                if (!$name) { echo "Название обязательно\n"; break; }
                echo "Описание (необязательно): ";
                $desc = trim(fgets(STDIN));
                echo "Частота (daily/weekly/monthly, по умолчанию daily): ";
                $freq = trim(fgets(STDIN));
                if (!$freq) $freq = 'daily';
                $h = addHabit($data, $name, $desc, $freq);
                echo "✅ Привычка #{$h['id']} '{$h['name']}' добавлена\n";
                break;
            case '2':
                if (empty($data['habits'])) {
                    echo "Нет привычек.\n";
                } else {
                    $today = date('Y-m-d');
                    printf("%-4s %-20s %-10s %s\n", "ID", "Название", "Частота", "Сегодня");
                    foreach ($data['habits'] as $h) {
                        $done = in_array($today, $h['history']) ? "✅" : "❌";
                        printf("%-4d %-20s %-10s %s\n", $h['id'], $h['name'], $h['frequency'], $done);
                    }
                }
                break;
            case '3':
                echo "ID привычки: ";
                $id = (int)trim(fgets(STDIN));
                if (!$id) { echo "Неверный ID\n"; break; }
                if (checkHabit($data, $id)) {
                    echo "✅ Привычка #$id отмечена выполнена\n";
                } else {
                    echo "❌ Не удалось отметить\n";
                }
                break;
            case '4':
                echo "ID привычки: ";
                $id = (int)trim(fgets(STDIN));
                if (!$id) { echo "Неверный ID\n"; break; }
                if (uncheckHabit($data, $id)) {
                    echo "✅ Отметка снята для #$id\n";
                } else {
                    echo "❌ Не удалось снять отметку\n";
                }
                break;
            case '5':
                echo "ID привычки: ";
                $id = (int)trim(fgets(STDIN));
                if (!$id) { echo "Неверный ID\n"; break; }
                $habit = null;
                foreach ($data['habits'] as $h) if ($h['id'] == $id) { $habit = $h; break; }
                if (!$habit) { echo "Привычка не найдена\n"; break; }
                $stats = getHabitStats($habit);
                echo "📊 Статистика для '{$habit['name']}':\n";
                echo "  Всего выполнений: {$stats['total']}\n";
                printf("  За последние 7 дней: %d (%.1f%%)\n", $stats['weekCount'], $stats['weekPercent']);
                echo "  Текущая серия (streak): {$stats['streak']} дней\n";
                echo "  Последнее выполнение: " . ($stats['lastCheck'] ?? 'никогда') . "\n";
                break;
            case '6':
                echo "ID привычки: ";
                $id = (int)trim(fgets(STDIN));
                if (!$id) { echo "Неверный ID\n"; break; }
                $habit = null;
                foreach ($data['habits'] as $h) if ($h['id'] == $id) { $habit = $h; break; }
                if (!$habit) { echo "Привычка не найдена\n"; break; }
                echo "Оставьте пустым, чтобы не менять.\n";
                echo "Название ({$habit['name']}): ";
                $newName = trim(fgets(STDIN));
                if ($newName === '') $newName = null;
                echo "Описание ({$habit['description']}): ";
                $newDesc = trim(fgets(STDIN));
                if ($newDesc === '') $newDesc = null;
                echo "Частота ({$habit['frequency']}): ";
                $newFreq = trim(fgets(STDIN));
                if ($newFreq === '') $newFreq = null;
                if (editHabit($data, $id, $newName, $newDesc, $newFreq)) {
                    echo "✅ Обновлено\n";
                } else {
                    echo "❌ Ошибка\n";
                }
                break;
            case '7':
                echo "ID для удаления: ";
                $id = (int)trim(fgets(STDIN));
                if (!$id) { echo "Неверный ID\n"; break; }
                if (deleteHabit($data, $id)) {
                    echo "✅ Удалено\n";
                } else {
                    echo "❌ Не найдено\n";
                }
                break;
            default:
                echo "Неверный выбор\n";
        }
    }
}

// ========== ВЕБ-ИНТЕРФЕЙС ==========
if (php_sapi_name() !== 'cli') {
    $data = loadData();
    ?>
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <title>🌱 Трекер привычек (PHP)</title>
        <style>
            body { font-family: 'Segoe UI', sans-serif; background: #f4f7fb; margin: 20px; }
            .container { max-width: 800px; margin: 0 auto; background: white; padding: 20px; border-radius: 16px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
            table { width: 100%; border-collapse: collapse; margin-top: 20px; }
            th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
            th { background: #2c3e50; color: white; }
            .form-row { margin: 10px 0; }
            .form-row label { display: inline-block; width: 100px; }
            input, select, button { padding: 6px; margin: 2px; }
            button { background: #3498db; color: white; border: none; border-radius: 4px; cursor: pointer; }
            .stats { margin-top: 20px; }
        </style>
    </head>
    <body>
    <div class="container">
        <h1>🌱 Трекер привычек</h1>
        <h3>Добавить привычку</h3>
        <form method="GET">
            <div class="form-row">
                <label>Название:</label><input type="text" name="name" required>
            </div>
            <div class="form-row">
                <label>Описание:</label><input type="text" name="description">
            </div>
            <div class="form-row">
                <label>Частота:</label>
                <select name="frequency">
                    <option value="daily">daily</option>
                    <option value="weekly">weekly</option>
                    <option value="monthly">monthly</option>
                </select>
            </div>
            <button type="submit" name="action" value="add">➕ Добавить</button>
        </form>

        <h3>Список привычек</h3>
        <table>
            <tr><th>ID</th><th>Название</th><th>Частота</th><th>Сегодня</th></tr>
            <?php
            $today = date('Y-m-d');
            foreach ($data['habits'] as $h):
                $done = in_array($today, $h['history']) ? "✅" : "❌";
            ?>
                <tr>
                    <td><?= $h['id'] ?></td>
                    <td><?= htmlspecialchars($h['name']) ?></td>
                    <td><?= $h['frequency'] ?></td>
                    <td><?= $done ?></td>
                </tr>
            <?php endforeach; ?>
        </table>

        <?php
        if (isset($_GET['action'])) {
            $action = $_GET['action'];
            if ($action == 'add' && isset($_GET['name'])) {
                $name = $_GET['name'];
                $desc = $_GET['description'] ?? '';
                $freq = $_GET['frequency'] ?? 'daily';
                addHabit($data, $name, $desc, $freq);
                echo "<div class='result' style='background:#d5f5e3; padding:10px; margin-top:10px;'>✅ Добавлено</div>";
                // перенаправляем, чтобы избежать повторного добавления
                header("Location: ?");
                exit;
            }
        }

        // Статистика
        if (!empty($data['habits'])) {
            echo "<div class='stats'><h3>📊 Статистика</h3>";
            foreach ($data['habits'] as $h) {
                $stats = getHabitStats($h);
                echo "<p><strong>{$h['name']}</strong><br>";
                echo "Всего: {$stats['total']}, за неделю: {$stats['weekCount']} ({$stats['weekPercent']:.1f}%), серия: {$stats['streak']} дней<br>";
                echo "Последнее выполнение: " . ($stats['lastCheck'] ?? 'никогда') . "</p>";
            }
            echo "</div>";
        }

        // Кнопки для отметок (упрощённо)
        ?>
        <h3>Отметить выполнение</h3>
        <form method="GET" style="display:inline-block;">
            <input type="hidden" name="action" value="check">
            <label>ID привычки: <input type="number" name="id" required></label>
            <button type="submit">✅ Отметить</button>
        </form>
        <form method="GET" style="display:inline-block;">
            <input type="hidden" name="action" value="uncheck">
            <label>ID привычки: <input type="number" name="id" required></label>
            <button type="submit">❌ Снять</button>
        </form>
        <?php
        if (isset($_GET['action']) && ($_GET['action'] == 'check' || $_GET['action'] == 'uncheck') && isset($_GET['id'])) {
            $id = (int)$_GET['id'];
            if ($_GET['action'] == 'check') {
                if (checkHabit($data, $id)) echo "<div style='color:green;'>✅ Отмечено</div>";
                else echo "<div style='color:red;'>❌ Ошибка</div>";
            } else {
                if (uncheckHabit($data, $id)) echo "<div style='color:green;'>✅ Снято</div>";
                else echo "<div style='color:red;'>❌ Ошибка</div>";
            }
        }
        ?>
    </div>
    </body>
    </html>
    <?php
}

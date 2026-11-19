<?php
/**
 * ActivityLogger
 * 
 * Central logger for recording admin actions into `admin_activities`.
 * Enforces a strict maximum of 10 rows in the database:
 * whenever a new activity is logged, any rows exceeding the latest 10
 * are automatically pruned from the database to prevent database bloat.
 */

require_once __DIR__ . '/Database.php';

class ActivityLogger
{
    /**
     * Log an admin activity and enforce 10-item cap in database.
     * 
     * @param string   $actionType  IMPORT, EXPORT, DELETE_LEADS, CLIENT_MANAGE, USER_MANAGE
     * @param string   $title       Short title for the action
     * @param string   $description Full detail of what was done
     * @param int|null $itemCount   Number of leads/records involved, if applicable
     * @param int|null $adminId     User ID who performed it
     * @param string|null $adminName Display name of the admin
     * @return int Inserted activity ID
     */
    public static function log(
        string $actionType,
        string $title,
        string $description,
        ?int $itemCount = null,
        ?int $adminId = null,
        ?string $adminName = null
    ): int {
        try {
            $db = Database::getConnection();

            // Auto-resolve admin name if not provided
            if (empty($adminName)) {
                if (!empty($adminId)) {
                    $stmtUser = $db->prepare("SELECT `full_name`, `username` FROM `users` WHERE `id` = ?");
                    $stmtUser->execute([$adminId]);
                    $row = $stmtUser->fetch();
                    if ($row) {
                        $adminName = !empty($row['full_name']) ? $row['full_name'] : $row['username'];
                    }
                }
                if (empty($adminName)) {
                    $adminName = 'System Administrator';
                }
            }

            // 1. Insert new activity
            $stmt = $db->prepare("
                INSERT INTO `admin_activities` 
                (`admin_id`, `admin_name`, `action_type`, `title`, `description`, `item_count`, `created_at`) 
                VALUES (?, ?, ?, ?, ?, ?, NOW())
            ");
            $stmt->execute([
                $adminId,
                $adminName,
                strtoupper(trim($actionType)),
                trim($title),
                trim($description),
                $itemCount
            ]);
            $newId = (int) $db->lastInsertId();

            // 2. Strict Database Auto-Prune:
            // Ensure only the latest 10 rows ever remain in the database table!
            $db->exec("
                DELETE FROM `admin_activities` 
                WHERE `id` NOT IN (
                    SELECT `id` FROM (
                        SELECT `id` FROM `admin_activities` ORDER BY `id` DESC LIMIT 10
                    ) AS keep_rows
                )
            ");

            return $newId;
        } catch (Exception $e) {
            error_log('ActivityLogger error: ' . $e->getMessage());
            return 0;
        }
    }

    /**
     * Returns the latest 10 activities formatted for frontend display.
     */
    public static function getLatest(int $limit = 10): array
    {
        try {
            $db = Database::getConnection();
            $stmt = $db->prepare("
                SELECT `id`, `admin_id`, `admin_name`, `action_type`, `title`, `description`, `item_count`, `created_at`
                FROM `admin_activities`
                ORDER BY `id` DESC
                LIMIT ?
            ");
            $stmt->bindValue(1, $limit, PDO::PARAM_INT);
            $stmt->execute();
            $rows = $stmt->fetchAll();

            $results = [];
            foreach ($rows as $row) {
                $results[] = [
                    'id'          => (int) $row['id'],
                    'admin_id'    => $row['admin_id'] ? (int) $row['admin_id'] : null,
                    'admin_name'  => $row['admin_name'],
                    'action_type' => $row['action_type'],
                    'title'       => $row['title'],
                    'description' => $row['description'],
                    'item_count'  => $row['item_count'] !== null ? (int) $row['item_count'] : null,
                    'created_at'  => $row['created_at'],
                    'date_formatted' => date('M d, Y • h:i A', strtotime($row['created_at'])),
                    'time_ago'    => self::timeAgo($row['created_at'])
                ];
            }

            return $results;
        } catch (Exception $e) {
            error_log('ActivityLogger getLatest error: ' . $e->getMessage());
            return [];
        }
    }

    /**
     * Human readable relative time format.
     */
    public static function timeAgo(string $datetime): string
    {
        $time = strtotime($datetime);
        $diff = time() - $time;

        if ($diff < 60) {
            return 'Just now';
        } elseif ($diff < 3600) {
            $mins = max(1, floor($diff / 60));
            return $mins . ' min' . ($mins > 1 ? 's' : '') . ' ago';
        } elseif ($diff < 86400) {
            $hours = floor($diff / 3600);
            return $hours . ' hour' . ($hours > 1 ? 's' : '') . ' ago';
        } elseif ($diff < 604800) {
            $days = floor($diff / 86400);
            return $days . ' day' . ($days > 1 ? 's' : '') . ' ago';
        } else {
            return date('M d, Y', $time);
        }
    }
}

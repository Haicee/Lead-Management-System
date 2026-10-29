<?php
/**
 * Migration Runner
 * 
 * A Laravel-like migration system for managing database schema changes.
 * 
 * Usage:
 *   php migrate.php migrate    — Run all pending migrations
 *   php migrate.php rollback   — Rollback the last batch of migrations
 *   php migrate.php status     — Show migration status
 *   php migrate.php fresh      — Drop all tables and re-run all migrations
 *   php migrate.php seed       — Run all seeders
 *   php migrate.php setup      — Run migrate + seed (fresh install)
 */

require_once __DIR__ . '/core/Database.php';

class MigrationRunner
{
    private PDO $db;
    private string $migrationsPath;
    private string $seedersPath;

    public function __construct()
    {
        $this->db = Database::getConnection();
        $this->migrationsPath = __DIR__ . '/database/migrations';
        $this->seedersPath = __DIR__ . '/database/seeders';
        $this->ensureMigrationsTable();
    }

    /**
     * Create the migrations tracking table if it doesn't exist.
     */
    private function ensureMigrationsTable(): void
    {
        $this->db->exec("
            CREATE TABLE IF NOT EXISTS `migrations` (
                `id` INT AUTO_INCREMENT PRIMARY KEY,
                `migration` VARCHAR(255) NOT NULL,
                `batch` INT NOT NULL,
                `executed_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");
    }

    /**
     * Get all migration files sorted by name.
     */
    private function getMigrationFiles(): array
    {
        $files = glob($this->migrationsPath . '/*.php');
        sort($files);
        return $files;
    }

    /**
     * Get already-ran migration names from the database.
     */
    private function getRanMigrations(): array
    {
        $stmt = $this->db->query("SELECT migration FROM migrations ORDER BY id");
        return $stmt->fetchAll(PDO::FETCH_COLUMN);
    }

    /**
     * Get the next batch number.
     */
    private function getNextBatch(): int
    {
        $stmt = $this->db->query("SELECT COALESCE(MAX(batch), 0) + 1 FROM migrations");
        return (int) $stmt->fetchColumn();
    }

    /**
     * Run all pending migrations.
     */
    public function migrate(): void
    {
        $files = $this->getMigrationFiles();
        $ran = $this->getRanMigrations();
        $batch = $this->getNextBatch();
        $pending = [];

        foreach ($files as $file) {
            $name = basename($file, '.php');
            if (!in_array($name, $ran)) {
                $pending[] = ['name' => $name, 'file' => $file];
            }
        }

        if (empty($pending)) {
            $this->info("Nothing to migrate. All migrations are up to date.");
            return;
        }

        foreach ($pending as $migration) {
            $this->info("Migrating: {$migration['name']}");

            $class = require $migration['file'];
            
            try {
                $class->up($this->db);

                $stmt = $this->db->prepare("INSERT INTO migrations (migration, batch) VALUES (?, ?)");
                $stmt->execute([$migration['name'], $batch]);

                $this->success("Migrated:  {$migration['name']}");
            } catch (Exception $e) {
                $this->error("Failed:    {$migration['name']} — " . $e->getMessage());
                return;
            }
        }

        $this->success("\n✓ All migrations completed successfully (Batch #{$batch}).");
    }

    /**
     * Rollback the last batch of migrations.
     */
    public function rollback(): void
    {
        $stmt = $this->db->query("SELECT COALESCE(MAX(batch), 0) FROM migrations");
        $lastBatch = (int) $stmt->fetchColumn();

        if ($lastBatch === 0) {
            $this->info("Nothing to rollback.");
            return;
        }

        $stmt = $this->db->prepare("SELECT migration FROM migrations WHERE batch = ? ORDER BY id DESC");
        $stmt->execute([$lastBatch]);
        $migrations = $stmt->fetchAll(PDO::FETCH_COLUMN);

        foreach ($migrations as $name) {
            $file = $this->migrationsPath . '/' . $name . '.php';
            if (file_exists($file)) {
                $this->info("Rolling back: {$name}");
                $class = require $file;
                
                try {
                    $class->down($this->db);
                    $delStmt = $this->db->prepare("DELETE FROM migrations WHERE migration = ?");
                    $delStmt->execute([$name]);
                    $this->success("Rolled back: {$name}");
                } catch (Exception $e) {
                    $this->error("Rollback failed: {$name} — " . $e->getMessage());
                    return;
                }
            }
        }

        $this->success("\n✓ Rollback completed (Batch #{$lastBatch}).");
    }

    /**
     * Drop all tables and re-run all migrations.
     */
    public function fresh(): void
    {
        $this->warning("Dropping all tables...");

        $this->db->exec("SET FOREIGN_KEY_CHECKS = 0");
        $stmt = $this->db->query("SHOW TABLES");
        $tables = $stmt->fetchAll(PDO::FETCH_COLUMN);

        foreach ($tables as $table) {
            $this->db->exec("DROP TABLE IF EXISTS `{$table}`");
            $this->info("  Dropped: {$table}");
        }
        $this->db->exec("SET FOREIGN_KEY_CHECKS = 1");

        $this->ensureMigrationsTable();
        $this->migrate();
    }

    /**
     * Show migration status.
     */
    public function status(): void
    {
        $files = $this->getMigrationFiles();
        $ran = $this->getRanMigrations();

        $this->info("\n Migration Status");
        $this->info(str_repeat('─', 60));

        foreach ($files as $file) {
            $name = basename($file, '.php');
            $status = in_array($name, $ran) ? '  ✓ Ran' : '  ✗ Pending';
            $color = in_array($name, $ran) ? '32' : '33';
            echo "  \033[{$color}m{$status}\033[0m  {$name}\n";
        }

        $this->info(str_repeat('─', 60));
        $this->info(sprintf("  Total: %d | Ran: %d | Pending: %d\n", 
            count($files), 
            count(array_intersect(array_map(fn($f) => basename($f, '.php'), $files), $ran)),
            count($files) - count(array_intersect(array_map(fn($f) => basename($f, '.php'), $files), $ran))
        ));
    }

    /**
     * Run all seeders.
     */
    public function seed(): void
    {
        $files = glob($this->seedersPath . '/*.php');
        sort($files);

        if (empty($files)) {
            $this->info("No seeders found.");
            return;
        }

        foreach ($files as $file) {
            $name = basename($file, '.php');
            $this->info("Seeding: {$name}");

            $class = require $file;
            
            try {
                $class->run($this->db);
                $this->success("Seeded:  {$name}");
            } catch (Exception $e) {
                $this->error("Seed failed: {$name} — " . $e->getMessage());
                return;
            }
        }

        $this->success("\n✓ All seeders completed successfully.");
    }

    /**
     * Fresh install: migrate + seed.
     */
    public function setup(): void
    {
        $this->info("══════════════════════════════════════");
        $this->info("  Lead Management System — Fresh Setup");
        $this->info("══════════════════════════════════════\n");
        $this->fresh();
        echo "\n";
        $this->seed();
        echo "\n";
        $this->success("✓ Setup complete! Your database is ready.");
    }

    // ── Console Output Helpers ──

    private function info(string $msg): void
    {
        echo "\033[36m{$msg}\033[0m\n";
    }

    private function success(string $msg): void
    {
        echo "\033[32m{$msg}\033[0m\n";
    }

    private function warning(string $msg): void
    {
        echo "\033[33m{$msg}\033[0m\n";
    }

    private function error(string $msg): void
    {
        echo "\033[31m{$msg}\033[0m\n";
    }
}

// ── CLI Entry Point ──

$runner = new MigrationRunner();
$command = $argv[1] ?? 'migrate';

match ($command) {
    'migrate'  => $runner->migrate(),
    'rollback' => $runner->rollback(),
    'status'   => $runner->status(),
    'fresh'    => $runner->fresh(),
    'seed'     => $runner->seed(),
    'setup'    => $runner->setup(),
    default    => print("\033[31mUnknown command: {$command}\n\nUsage: php migrate.php [migrate|rollback|status|fresh|seed|setup]\033[0m\n"),
};

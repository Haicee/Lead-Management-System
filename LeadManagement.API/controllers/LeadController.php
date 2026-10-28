<?php
/**
 * Lead Controller
 * 
 * Handles batch lead imports, querying leads, and import audit history.
 */

class LeadController
{
    private PDO $db;

    public function __construct()
    {
        $this->db = Database::getConnection();
    }

    /**
     * POST /leads/import
     * 
     * Stores an imported batch and all parsed lead records in the database.
     * Uses a PDO transaction to guarantee atomic persistence.
     * 
     * Expected JSON Payload:
     * {
     *   "batch_name": "Nebraska_October_2026",
     *   "file_name": "Nebraska_Restaurant_Leads_2026.csv",
     *   "initial_status": "New Lead",
     *   "user_id": 1, (optional)
     *   "leads": [ ... ]
     * }
     */
    public function import(array $input): void
    {
        $batchName = trim($input['batch_name'] ?? '');
        $fileName = trim($input['file_name'] ?? '');
        $initialStatus = trim($input['initial_status'] ?? 'New Lead');
        $userId = !empty($input['user_id']) ? (int) $input['user_id'] : null;
        $leads = $input['leads'] ?? [];

        if (empty($batchName)) {
            $batchName = 'Batch_' . date('Ymd_His');
        }

        if (empty($fileName)) {
            $fileName = 'Direct_Import_' . date('Y-m-d');
        }

        if (!is_array($leads) || empty($leads)) {
            Response::error('No lead records provided for import.', 422);
        }

        $totalLeads = count($leads);

        try {
            $this->db->beginTransaction();

            // 1. Create the Import Batch Audit Record
            $stmtBatch = $this->db->prepare("
                INSERT INTO `import_batches` 
                (`batch_name`, `file_name`, `total_leads`, `initial_status`, `imported_by`) 
                VALUES (?, ?, ?, ?, ?)
            ");
            $stmtBatch->execute([$batchName, $fileName, $totalLeads, $initialStatus, $userId]);
            $batchId = (int) $this->db->lastInsertId();

            // 2. Prepare the Lead Insert Statement
            $stmtLead = $this->db->prepare("
                INSERT INTO `leads` (
                    `batch_id`, `batch_name`, `status`, `business_name`, `address`, 
                    `city`, `county`, `state`, `zip_code`, `cell_number`, 
                    `fax`, `annual_sales`, `number_of_employees`, `title`, `contact_name`, 
                    `website`, `sic_code`, `sic_description`, `naics_code`, `naics_description`
                ) VALUES (
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?
                )
            ");

            // 3. Insert each lead
            $inserted = 0;
            foreach ($leads as $lead) {
                $status = !empty($lead['status']) ? $lead['status'] : $initialStatus;
                $businessName = trim($lead['business_name'] ?? '');

                // Fallback if business name empty
                if (empty($businessName)) {
                    $businessName = 'Unnamed Lead #' . ($inserted + 1);
                }

                $stmtLead->execute([
                    $batchId,
                    $batchName,
                    $status,
                    $businessName,
                    trim($lead['address'] ?? ''),
                    trim($lead['city'] ?? ''),
                    trim($lead['county'] ?? ''),
                    trim($lead['state'] ?? 'NE'),
                    trim($lead['zip_code'] ?? ''),
                    trim($lead['cell_number'] ?? ''),
                    trim($lead['fax'] ?? ''),
                    trim($lead['annual_sales'] ?? 'Less than 500,000'),
                    trim($lead['number_of_employees'] ?? '1 to 4'),
                    trim($lead['title'] ?? ''),
                    trim($lead['contact_name'] ?? ''),
                    trim($lead['website'] ?? ''),
                    trim($lead['sic_code'] ?? ''),
                    trim($lead['sic_description'] ?? ''),
                    trim($lead['naics_code'] ?? ''),
                    trim($lead['naics_description'] ?? '')
                ]);
                $inserted++;
            }

            $this->db->commit();

            Response::success([
                'batch_id'       => $batchId,
                'batch_name'     => $batchName,
                'inserted_count' => $inserted,
                'total_leads'    => $totalLeads
            ], "Successfully imported {$inserted} leads into the database.", 201);

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to import leads: ' . $e->getMessage());
        }
    }

    /**
     * GET /leads
     * 
     * Returns leads with optional filtering and pagination.
     */
    public function index(): void
    {
        $limit = isset($_GET['limit']) ? (int) $_GET['limit'] : 5000;
        $offset = isset($_GET['offset']) ? max((int) $_GET['offset'], 0) : 0;
        $status = $_GET['status'] ?? null;
        $city = $_GET['city'] ?? null;
        $search = $_GET['search'] ?? null;
        $batchId = $_GET['batch_id'] ?? null;

        $conditions = [];
        $params = [];

        if (!empty($batchId)) {
            $conditions[] = "`batch_id` = ?";
            $params[] = (int) $batchId;
        }

        if (!empty($status) && $status !== 'All Statuses') {
            $conditions[] = "`status` = ?";
            $params[] = $status;
        }

        if (!empty($city) && $city !== 'All Cities') {
            $conditions[] = "`city` = ?";
            $params[] = $city;
        }

        if (!empty($search)) {
            $conditions[] = "(`business_name` LIKE ? OR `contact_name` LIKE ? OR `cell_number` LIKE ? OR `city` LIKE ?)";
            $term = '%' . $search . '%';
            $params[] = $term;
            $params[] = $term;
            $params[] = $term;
            $params[] = $term;
        }

        $whereClause = !empty($conditions) ? 'WHERE ' . implode(' AND ', $conditions) : '';

        // Total count
        $countStmt = $this->db->prepare("SELECT COUNT(*) FROM `leads` {$whereClause}");
        $countStmt->execute($params);
        $totalCount = (int) $countStmt->fetchColumn();

        // Data rows
        $limitClause = $limit > 0 ? "LIMIT {$limit} OFFSET {$offset}" : "";
        $sql = "
            SELECT * FROM `leads` 
            {$whereClause} 
            ORDER BY `id` ASC 
            {$limitClause}
        ";
        $stmt = $this->db->prepare($sql);
        $stmt->execute($params);
        $leads = $stmt->fetchAll();

        Response::success([
            'total'  => $totalCount,
            'limit'  => $limit,
            'offset' => $offset,
            'leads'  => $leads
        ], 'Leads retrieved successfully');
    }

    /**
     * GET /leads/filters
     * 
     * Returns distinct cities and statuses present in the database.
     */
    public function filters(): void
    {
        $citiesStmt = $this->db->query("SELECT DISTINCT `city` FROM `leads` WHERE `city` IS NOT NULL AND `city` != '' ORDER BY `city` ASC");
        $cities = $citiesStmt->fetchAll(PDO::FETCH_COLUMN);

        $statusesStmt = $this->db->query("SELECT DISTINCT `status` FROM `leads` WHERE `status` IS NOT NULL AND `status` != '' ORDER BY `status` ASC");
        $statuses = $statusesStmt->fetchAll(PDO::FETCH_COLUMN);

        Response::success([
            'cities'   => $cities,
            'statuses' => $statuses
        ], 'Filter options retrieved');
    }

    /**
     * GET /leads/stats
     * 
     * Returns summary dashboard counts.
     */
    public function stats(): void
    {
        $totalLeads = (int) $this->db->query("SELECT COUNT(*) FROM `leads`")->fetchColumn();
        $newLeads = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE `status` = 'New Lead'")->fetchColumn();
        $qualified = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE `status` = 'Qualified'")->fetchColumn();
        $contacted = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE `status` = 'Contacted'")->fetchColumn();

        Response::success([
            'total_leads'     => $totalLeads,
            'new_leads'       => $newLeads,
            'qualified_leads' => $qualified,
            'contacted_leads' => $contacted
        ], 'Lead metrics retrieved');
    }

    /**
     * GET /batches
     * 
     * Returns all past import batches for Import History.
     */
    public function batches(): void
    {
        $stmt = $this->db->query("
            SELECT b.*, u.username as imported_by_username 
            FROM `import_batches` b
            LEFT JOIN `users` u ON b.imported_by = u.id
            ORDER BY b.id DESC
        ");

        Response::success($stmt->fetchAll(), 'Import batches retrieved');
    }

    /**
     * GET /batches/{id}/leads
     * 
     * Returns all leads belonging to a specific batch.
     */
    public function batchLeads(int $batchId): void
    {
        if ($batchId <= 0) {
            Response::error('Invalid batch ID.', 422);
        }

        $stmt = $this->db->prepare("
            SELECT * FROM `leads` 
            WHERE `batch_id` = ? 
            ORDER BY `id` ASC
        ");
        $stmt->execute([$batchId]);
        $leads = $stmt->fetchAll();

        Response::success([
            'batch_id' => $batchId,
            'total'    => count($leads),
            'leads'    => $leads
        ], 'Batch leads retrieved successfully');
    }

    /**
     * DELETE /batches/{id} or POST /batches/delete
     * 
     * Deletes a batch and all associated leads permanently.
     */
    public function deleteBatch(int $batchId): void
    {
        if ($batchId <= 0) {
            Response::error('Invalid batch ID.', 422);
        }

        // Verify batch exists
        $stmt = $this->db->prepare("SELECT id, batch_name, total_leads FROM `import_batches` WHERE id = ?");
        $stmt->execute([$batchId]);
        $batch = $stmt->fetch();

        if (!$batch) {
            Response::notFound('Batch not found.');
        }

        try {
            $this->db->beginTransaction();

            // Explicitly delete leads for this batch (cascade also covers it)
            $delLeads = $this->db->prepare("DELETE FROM `leads` WHERE `batch_id` = ?");
            $delLeads->execute([$batchId]);
            $deletedLeadsCount = $delLeads->rowCount();

            // Delete the batch record
            $delBatch = $this->db->prepare("DELETE FROM `import_batches` WHERE `id` = ?");
            $delBatch->execute([$batchId]);

            $this->db->commit();

            Response::success([
                'batch_id'      => $batchId,
                'batch_name'    => $batch['batch_name'],
                'deleted_leads' => $deletedLeadsCount
            ], "Batch '{$batch['batch_name']}' and its {$deletedLeadsCount} leads were deleted successfully.");

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to delete batch: ' . $e->getMessage());
        }
    }
}

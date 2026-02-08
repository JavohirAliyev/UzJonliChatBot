let currentPage = 1;
let totalPages = 1;
let currentSearch = '';

// Check if already logged in
window.onload = function() {
    const token = localStorage.getItem('adminToken');
    if (token) {
        showDashboard();
        loadUsers();
    }
};

async function login(event) {
    event.preventDefault();
    
    const username = document.getElementById('username').value;
    const password = document.getElementById('password').value;
    const errorDiv = document.getElementById('loginError');

    try {
        const response = await fetch('/api/admin/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });

        if (response.ok) {
            const data = await response.json();
            localStorage.setItem('adminToken', data.token);
            showDashboard();
            loadUsers();
        } else {
            errorDiv.textContent = 'Invalid username or password';
            errorDiv.classList.remove('hidden');
        }
    } catch (error) {
        errorDiv.textContent = 'Login failed. Please try again.';
        errorDiv.classList.remove('hidden');
    }
}

function logout() {
    localStorage.removeItem('adminToken');
    location.reload();
}

function showDashboard() {
    document.getElementById('loginSection').classList.add('hidden');
    document.getElementById('dashboardSection').classList.remove('hidden');
    document.getElementById('logoutBtn').classList.remove('hidden');
}

async function loadUsers(page = 1) {
    const token = localStorage.getItem('adminToken');
    if (!token) return;

    document.getElementById('loadingIndicator').classList.remove('hidden');
    document.getElementById('usersTable').classList.add('hidden');

    try {
        const url = new URL('/api/admin/users', window.location.origin);
        url.searchParams.append('page', page);
        url.searchParams.append('pageSize', 20);
        if (currentSearch) {
            url.searchParams.append('search', currentSearch);
        }

        const response = await fetch(url, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.status === 401) {
            logout();
            return;
        }

        if (response.ok) {
            const data = await response.json();
            displayUsers(data.users);
            currentPage = data.page;
            totalPages = data.totalPages;
            renderPagination();
            document.getElementById('usersTable').classList.remove('hidden');
        }
    } catch (error) {
        console.error('Error loading users:', error);
    } finally {
        document.getElementById('loadingIndicator').classList.add('hidden');
    }
}

function displayUsers(users) {
    const tbody = document.getElementById('usersTableBody');
    tbody.innerHTML = '';

    users.forEach(user => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${user.telegramId}</td>
            <td>${user.gender || 'N/A'}</td>
            <td>
                <span class="badge ${user.isAgeVerified ? 'badge-success' : 'badge-warning'}">
                    ${user.isAgeVerified ? 'Yes' : 'No'}
                </span>
            </td>
            <td>${user.registrationStatus}</td>
            <td>
                <span class="badge ${user.isBanned ? 'badge-danger' : 'badge-success'}">
                    ${user.isBanned ? 'Banned' : 'Active'}
                </span>
            </td>
            <td>${new Date(user.createdAt).toLocaleDateString()}</td>
            <td>
                ${user.isBanned 
                    ? `<button class="action-btn unban-btn" onclick="unbanUser(${user.telegramId})">Unban</button>`
                    : `<button class="action-btn ban-btn" onclick="banUser(${user.telegramId})">Ban</button>`
                }
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderPagination() {
    const pagination = document.getElementById('pagination');
    pagination.innerHTML = '';

    for (let i = 1; i <= totalPages; i++) {
        const btn = document.createElement('button');
        btn.className = `page-btn ${i === currentPage ? 'active' : ''}`;
        btn.textContent = i;
        btn.onclick = () => loadUsers(i);
        pagination.appendChild(btn);
    }
}

async function banUser(telegramId) {
    if (!confirm(`Are you sure you want to ban user ${telegramId}?`)) return;

    const token = localStorage.getItem('adminToken');
    try {
        const response = await fetch(`/api/admin/users/${telegramId}/ban`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            loadUsers(currentPage);
        }
    } catch (error) {
        console.error('Error banning user:', error);
    }
}

async function unbanUser(telegramId) {
    if (!confirm(`Are you sure you want to unban user ${telegramId}?`)) return;

    const token = localStorage.getItem('adminToken');
    try {
        const response = await fetch(`/api/admin/users/${telegramId}/unban`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            loadUsers(currentPage);
        }
    } catch (error) {
        console.error('Error unbanning user:', error);
    }
}

function searchUsers() {
    currentSearch = document.getElementById('searchInput').value;
    currentPage = 1;
    loadUsers(1);
}

// Allow search on Enter key
document.getElementById('searchInput')?.addEventListener('keypress', function(e) {
    if (e.key === 'Enter') {
        searchUsers();
    }
});

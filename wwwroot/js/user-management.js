// File: wwwroot/js/user-management.js

// Modern non-deprecated document ready syntax
$(function () {
    // Tab initialization from URL function
    function initTabFromUrl() {
        // Get the 'tab' parameter from URL
        const urlParams = new URLSearchParams(window.location.search);
        const tabParam = urlParams.get('tab');

        // If tab parameter exists and is valid, activate that tab
        if (tabParam) {
            const tabBtn = $(`.tab-btn[data-tab="${tabParam}"]`);
            if (tabBtn.length) {
                // Trigger click event programmatically using modern approach
                tabBtn.trigger('click');
            }
        }
    }

    // Initialize tab from URL parameter
    initTabFromUrl();

    // Tab switching functionality
    $('.tab-btn').on('click', function () {
        const tabId = $(this).data('tab');

        // Update active tab button
        $('.tab-btn').removeClass('active');
        $(this).addClass('active');

        // Show corresponding tab content
        $('.tab-pane').removeClass('active');
        $(`#${tabId}`).addClass('active');

        // Update URL with tab parameter without page reload
        const url = new URL(window.location);
        url.searchParams.set('tab', tabId);
        window.history.pushState({}, '', url);
    });

    // User search functionality
    $('#user-search').on('keyup', function () {
        const searchTerm = $(this).val().toLowerCase();

        $('.data-table tbody tr').each(function () {
            const userName = $(this).find('.user-name').text().toLowerCase();
            const userEmail = $(this).find('td:nth-child(3)').text().toLowerCase();
            const userRole = $(this).find('td:nth-child(4)').text().toLowerCase();

            if (userName.includes(searchTerm) || userEmail.includes(searchTerm) || userRole.includes(searchTerm)) {
                $(this).show();
            } else {
                $(this).hide();
            }
        });
    });

    // Filter functionality
    $('#role-filter, #status-filter').on('change', function () {
        applyFilters();
    });

    function applyFilters() {
        const roleFilter = $('#role-filter').val();
        const statusFilter = $('#status-filter').val();

        $('.data-table tbody tr').each(function () {
            const userRole = $(this).find('td:nth-child(4)').text();
            const userStatus = $(this).find('td:nth-child(5) .badge').text();

            const roleMatch = roleFilter === 'all' || userRole === roleFilter;
            const statusMatch = statusFilter === 'all' || userStatus === statusFilter;

            if (roleMatch && statusMatch) {
                $(this).show();
            } else {
                $(this).hide();
            }
        });
    }

    // Select all users checkbox
    $('#select-all-users').on('change', function () {
        const isChecked = $(this).prop('checked');
        $('.user-checkbox').prop('checked', isChecked);
        updateBulkActions();
    });

    // Individual user checkboxes
    $('.user-checkbox').on('change', function () {
        updateBulkActions();
    });

    function updateBulkActions() {
        const checkedCount = $('.user-checkbox:checked').length;
        $('#apply-bulk').prop('disabled', checkedCount === 0);
    }

    // Apply bulk actions
    $('#apply-bulk').on('click', function () {
        const action = $('#bulk-action').val();
        if (!action) return;

        const selectedUserIds = [];
        $('.user-checkbox:checked').each(function () {
            selectedUserIds.push($(this).val());
        });

        if (action === 'delete') {
            if (confirm(`Are you sure you want to delete ${selectedUserIds.length} user(s)? This action cannot be undone.`)) {
                submitBulkAction(action, selectedUserIds);
            }
        } else {
            submitBulkAction(action, selectedUserIds);
        }
    });

    function submitBulkAction(action, userIds) {
        $.ajax({
            url: '/Admin/UserManagement/BulkAction',
            type: 'POST',
            data: {
                action: action,
                userIds: userIds
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);
                    // Reload page after successful action
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'An error occurred while processing your request.');
            }
        });
    }

    // User action buttons
    $('.reset-password-btn').on('click', function () {
        const userId = $(this).data('user-id');
        $('#reset-user-id').val(userId);
        $('#reset-password-modal').addClass('show');
    });

    $('.suspend-user-btn').on('click', function () {
        const userId = $(this).data('user-id');
        if (confirm('Are you sure you want to suspend this user?')) {
            changeUserStatus(userId, 'Suspended');
        }
    });

    $('.activate-user-btn').on('click', function () {
        const userId = $(this).data('user-id');
        if (confirm('Are you sure you want to activate this user?')) {
            changeUserStatus(userId, 'Active');
        }
    });

    $('.delete-user-btn').on('click', function () {
        const userId = $(this).data('user-id');
        $('#confirm-delete').data('user-id', userId);
        $('#delete-confirmation-modal').addClass('show');
    });

    // Pending request actions
    $('.approve-request-btn').on('click', function () {
        const userId = $(this).data('user-id');
        const notes = $(`.request-note-text[data-user-id="${userId}"]`).val();

        if (confirm('Are you sure you want to approve this user?')) {
            approveUser(userId, notes);
        }
    });

    $('.reject-request-btn').on('click', function () {
        const userId = $(this).data('user-id');
        const notes = $(`.request-note-text[data-user-id="${userId}"]`).val();

        if (confirm('Are you sure you want to reject this user request?')) {
            rejectUser(userId, notes);
        }
    });

    $('.request-more-info-btn').on('click', function () {
        const userId = $(this).data('user-id');
        const notes = $(`.request-note-text[data-user-id="${userId}"]`).val();

        if (notes.trim() === '') {
            alert('Please provide details about what additional information is needed.');
            return;
        }

        requestMoreInfo(userId, notes);
    });

    function approveUser(userId, notes) {
        $.ajax({
            url: '/UserManagement/ProcessPendingUser',
            type: 'POST',
            data: {
                userId: userId,
                action: 'approve',
                notes: notes
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'An error occurred while processing your request.');
            }
        });
    }

    function rejectUser(userId, notes) {
        $.ajax({
            url: '/UserManagement/ProcessPendingUser',
            type: 'POST',
            data: {
                userId: userId,
                action: 'reject',
                notes: notes
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'An error occurred while processing your request.');
            }
        });
    }

    function requestMoreInfo(userId, notes) {
        $.ajax({
            url: '/UserManagement/ProcessPendingUser',
            type: 'POST',
            data: {
                userId: userId,
                action: 'request-more-info',
                notes: notes
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'An error occurred while processing your request.');
            }
        });
    }

    function changeUserStatus(userId, status) {
        $.ajax({
            url: '/Admin/UserManagement/ChangeUserStatus',
            type: 'POST',
            data: {
                userId: userId,
                status: status
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);
                    // Reload page after successful action
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'An error occurred while processing your request.');
            }
        });
    }

    // Legacy pending request actions (kept for backward compatibility)
    $('#approve-request').on('click', function () {
        const userId = $(this).data('user-id');
        const notes = $(`.request-note-text[data-user-id="${userId}"]`).val();

        $.ajax({
            url: '/Admin/UserManagement/ApproveUser',
            type: 'POST',
            data: {
                userId: userId,
                notes: notes
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);
                    // Reload page after successful action
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'An error occurred while processing your request.');
            }
        });
    });

    // Role management
    $('.view-role-btn').on('click', function () {
        const roleName = $(this).data('role-name');
        loadRolePermissions(roleName);
    });

    function loadRolePermissions(roleName) {
        $.ajax({
            url: '/UserManagement/GetRolePermissions', // Update path if needed
            type: 'GET',
            data: {
                roleName: roleName
            },
            success: function (response) {
                if (response.success) {
                    const template = $('#role-permissions-template').html();
                    const rendered = Mustache.render(template, response.data);
                    $('#permissions-content').html(rendered);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function (xhr, status, error) {
                console.error('Error loading role permissions:', error);
                showAlert('danger', 'Error loading role permissions: ' + error);
            }
        });
    }

    // Modal functionality
    $('.close-modal, .close-modal-btn').on('click', function () {
        $(this).closest('.modal').removeClass('show');
    });

    // Show alert message
    function showAlert(type, message) {
        const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
        const icon = type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle';

        const alertHtml = `
            <div class="alert ${alertClass}">
                <i class="fas ${icon}"></i> ${message}
                <button class="close-alert"><i class="fas fa-times"></i></button>
            </div>
        `;

        $('.page-header').after(alertHtml);

        // Auto hide alert after 5 seconds
        setTimeout(function () {
            $('.alert').fadeOut(300, function () {
                $(this).remove();
            });
        }, 5000);
    }

    // Close alert when clicking the X button
    $(document).on('click', '.close-alert', function () {
        $(this).parent().fadeOut(300, function () {
            $(this).remove();
        });
    });

    // Debug logging for form submission
    console.log('User management script loaded');

    // Log when the form exists
    if ($('#user-form').length) {
        console.log('User form found on page');

        // Attach submission event logging
        $('#user-form').on('submit', function () {
            console.log('User form submitted');
        });
    } else {
        console.log('WARNING: User form not found!');
    }

    // Log ModelState errors if they exist
    if ($('.validation-summary-errors').length) {
        console.log('Validation errors exist:');
        $('.validation-summary-errors ul li').each(function () {
            console.log(' - ' + $(this).text());
        });
    }

    // Check TempData messages
    if ($('.alert').length) {
        console.log('Alert messages found:');
        $('.alert').each(function () {
            console.log(' - ' + $(this).text().trim());
        });
    }

    // Form submission handler
    $('.user-form, #user-form').on('submit', function (e) {
        console.log('Form submit event triggered');

        // Disable submit button to prevent double submissions
        $(this).find('button[type="submit"]').prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Saving...');

        // Let the form submit normally
        return true;
    });

    // Debug logging for form elements
    if ($('form.user-form').length) {
        console.log('Form with class user-form found');
    } else {
        console.log('WARNING: Form with class user-form not found');
    }

    if ($('#user-form').length) {
        console.log('Form with ID user-form found');
    } else {
        console.log('WARNING: Form with ID user-form not found');
    }

    // Check if the form has correct action
    let formAction = $('form.user-form').attr('action');
    console.log('Form action attribute:', formAction);

    // Check for required fields
    let requiredFields = $('form.user-form input[required], form.user-form select[required]');
    console.log('Required fields count:', requiredFields.length);

    // Check for antiforgery token
    if ($('form.user-form input[name="__RequestVerificationToken"]').length) {
        console.log('Antiforgery token found');
    } else {
        console.log('WARNING: Antiforgery token missing');
    }
});
// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

$(document).ready(function () {
    if ($('#deliverablesTable').length) {
        console.log("Initializing DataTable");
        $('#deliverablesTable').DataTable({
            order: [[3, 'desc']]
        }); // ✅ Should now work
    } else {
        console.warn("Table #deliverablesTable not found");
    }
    $('#deliverablesTable').on('click', '.generateAI', function (e) {
        e.preventDefault();
        const id = $(this).data("id");
        console.log("Generating AI for ID:", id);
        // Resize the table container
        $('#ai_table_container')
            .removeClass('col-md-12')
            .addClass('col-md-7');

        // Show the AI form container
        $('#ai_form_container')
            .removeClass('d-none')
            .addClass('col-md-5');
        //// Store reference to the clicked row
        //const $button = $(this);
        //const $row = $button.closest("tr");
        //const year = $('#yearFilter').val();
        //console.log("Year : " + year);
        //$('#loadingOverlay').show();
        //$('#showMessage').html("");
        //$.ajax({
        //    url: `/AIDeliverable/${year}/${id}`,
        //    type: "GET",
        //    success: function (response) {
        //        $('#loadingOverlay').hide();
        //        $('#showMessage').html('<div class="alert alert-success alert-dismissible fade show material-shadow" role="alert">' +
        //            '<b> AI content generated successfully!</b>' +
        //            '<button type = "button" class= "btn-close" data-bs-dismiss="alert" aria-label="Close"></button></div> ');
        //        console.log(response);
        //        var bookmarkKey = Object.keys(response.bookmark)[0];
        //        var bookmarkValue = response.bookmark[bookmarkKey];
        //        console.log(bookmarkValue);
        //        var table = $('#deliverablesTable').DataTable();
        //        var rowIndex = table.row($row).index(); // get row index
        //        var rowData = table.row(rowIndex).data(); // get data array

        //        // Set the 8th column (index 7) to the bookmark link
        //        rowData[5] = `<a href="${bookmarkValue}" target="_blank">Google Bookmark</a>`;

        //        // Update the row and redraw
        //        table.row(rowIndex).data(rowData).draw();
        //        var rowNode = table.row(rowIndex).node();
        //        $(rowNode).addClass('table-success');
        //    },
        //    error: function () {
        //        $('#loadingOverlay').hide();
        //        $('#showMessage').html('<div class="alert alert-danger alert-dismissible fade show material-shadow" role="alert">' +
        //            '<b> Error generating AI</b>!' +
        //            '<button type = "button" class= "btn-close" data-bs-dismiss="alert" aria-label="Close"></button></div> ');
        //    }
        //});
    });
});

function loadDeliverablesByYear() {
    const year = $('#yearFilter').val();
    const account_id = $('#AccountId').val();

    $.ajax({
        url: '/GetDeliverablesByYear/' + account_id + '/' + year,
        type: 'GET',
        success: function (data) {
            $('.loader-wrapper').css("display", 'none');
            $('#deliverablesTable').DataTable().clear().draw();
            $('#deliverablesTable').DataTable().destroy();
            if (data.length > 0) {
                oTable = $('#deliverablesTable').DataTable({
                    "paging": true,
                    "searching": true,
                    "info": true,
                    "responsive": true,
                    "lengthChange": false,
                    "autoWidth": true,
                    "scrollX": true,
                    dom: 'Bfrtip',
                    order: [[0, "desc"]],
                    buttons: [
                        'copyHtml5',
                        'excelHtml5',
                        'csvHtml5',
                        'pdfHtml5'
                    ],
                    "data": data,
                    "columns": [
                        { data: null, render: function (data, type, row) { return row.quarter; } },
                        { data: null, render: function (data, type, row) { return row.block; } },
                        { data: null, render: function (data, type, row) { return row.topic_Category; } },
                        { data: null, render: function (data, type, row) { return row.main_Status; } },
                        { data: null, render: function (data, type, row) { return row.name; } },
                        {
                            data: null, render: function (data, type, row) {
                                return '<a href="${deliverable.gDocs_Content}" target="_blank">Google URL</a>';
                            }
                        },
                        {
                            data: null, render: function (data, type, row) {
                                return '<a class="btn btn - primary generateAI" data-id="' + row.id + '">Generate AI</a>';
                            }
                        },
                    ],
                    "fnInitComplete": function () { $("#deliverablesTable").css("width", "100%"); }
                }).buttons().container().appendTo('#deliverablesTable_wrapper .col-md-6:eq(0)');
                oTable = $('#deliverablesTable').DataTable();
            } else {
                $('.loader-wrapper').css("display", 'none');
                oTable = $('#deliverablesTable').DataTable();
            }
        },
        error: function () {
            alert("Failed to load data.");
        }
    });
}
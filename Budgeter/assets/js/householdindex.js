$(function() {
    var postaction;
    var currentTr;
    var id;

    //Click event for deleting a row from the accounts table
    $(document).on("click", ".delete", function () {
        debugger;
        event.preventDefault();
        //Gets link, splits it where there are "/" and then "pops" the last part of the string into the variable id
        postaction = $(this).attr("href");
        id = postaction.split('/').pop();
        //Gets current tr clicked
        currentTr = $(this).closest('tr');
        //grabs name from tr
        var currentName = currentTr.siblings(":first").text();
        $('#myModalLabel').text(currentName);
        //Opens modal for deleting accounts
        $('#deleteModal').modal('show');
    });

    $('#del').on("click", function () {
        debugger;
        event.preventDefault();
        deleteAccount();
        $('#deleteModal').modal('hide');
    });

    function deleteAccount() {
        $.ajax({
            type: "POST",
            url: postaction,
            data: id,
            success: function (data) {
                debugger;
                if (data.success == false) {
                    alert(data.errorMessage);
                }
                else {
                    //Wraps children of tr in div tags, so that we'll be able to animate the td more successfully
                    currentTr.children('td').wrapInner('<div/>');
                    currentTr.children('td').children('div').slideUp('fast', function () { currentTr.remove() });
                    $.post('@Url.Action("GetBarChart", "Home")').then(function (response) {
                        barChart.setData(response);
                    });
                }
            },
            error: function () {
                alert("Post action failed");
            }
        });
    };
});
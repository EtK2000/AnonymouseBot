/// <reference path="jquery-1.9.1.intellisense.js" />

var pirates = {};

$(function ()
{
    pirates.menu.init();
    pirates.manage_team.init();
    pirates.scores.init();
    pirates.trytowin.init();
    pirates.preloader.init();
    pirates.game.init();

    $(window).resize();
});

pirates.game = {
    init: function ()
    {
        if ($(".page_game").length == 0)
            return;

        $(".page_game .btn-debug").click(function ()
        {
            $(".page_game").toggleClass("game").toggleClass("debug");
        });

        $(window).resize(function ()
        {
            var height = $(window).height() - $(".page_game .player").outerHeight(true) - $(".page_game .score").outerHeight(true) - 10;
            $(".page_game .board").height(height).width(height);
            $(".page_game .water").width(height + (84 * 2)).height(height + (54 * 2));
            $(".page_game .code").height(height - (54 * 1));
        });
    }
};

pirates.preloader = {
    init: function ()
    {
        if ($(".page_preloader").length == 0)
            return;

        setInterval(function ()
        {
            var time = parseInt($(".page_preloader .time").text(), 10);
            $(".page_preloader .time").text(time - 1);

            if (time == 1)
            {
                window.location = $("#navigate_url").attr("href");
            }
        }, 1000);
    }
};

pirates.scores = {
    index: 0,
    size: 6,
    count: 0,

    init: function ()
    {
        if ($(".page_scores").length == 0)
            return;

        pirates.scores.count = Math.ceil($(".page_scores .table th.round").length * 1.0 / 6);

        $(".page_scores_header .btn").click(function (e)
        {
            e.preventDefault();

            if ($(this).hasClass("btn-next") == true)
            {
                pirates.scores.index += 1;
                if (pirates.scores.index > pirates.scores.count)
                    pirates.scores.index = 1;
            }
            else
            {
                pirates.scores.index -= 1;
                if (pirates.scores.index == 0)
                    pirates.scores.index = pirates.scores.count;
            }

            var startIndex = (pirates.scores.index - 1) * pirates.scores.size;
            var endIndex = pirates.scores.index * pirates.scores.size;
            $(".page_scores .table thead th.round").hide();
            $(".page_scores .table thead th.round").slice(startIndex, endIndex).show();
            $(".page_scores .table tbody tr").each(function ()
            {
                $(this).find("td.round").hide();
                $(this).find("td.round").slice(startIndex, endIndex).show();
            });
        });

        $(".page_scores_header .btn-next").click();
    }
};

pirates.menu = {
    init: function ()
    {
        $(window).resize(function ()
        {
            $(".menu").css("min-height", $(document).height());
        });

        $(".menu .button").click(function ()
        {
            var width = $(".menu").hasClass("show") ? "90px" : "220px";

            $(".menu").toggleClass("show")
            if ($(".menu").hasClass("show") == false)
                $(".menu li a span").toggle();

            $(".menu").animate({
                width: width
            }, function ()
            {
                if ($(".menu").hasClass("show") == true)
                    $(".menu li a span").toggle();
            });
        });
    }
};

pirates.trytowin = {
    init: function ()
    {
        if ($(".page_try_to_win").length == 0)
            return;

        var $avatar = $(".page_try_to_win .avatars #avatar");

        //events
        $('.page_try_to_win .avatars #avatars-carousel').on('slid.bs.carousel', function ()
        {
            var value = $(".page_try_to_win .avatars .item.active").attr("data-value");
            $avatar.val(value);
        });

        //init avatar
        $(".page_try_to_win .avatars .item").removeClass("active");
        $(".page_try_to_win .avatars .item[data-value='" + $avatar.val() + "']").addClass("active");
    }
};

pirates.manage_team = {
    init: function ()
    {
        if ($(".page_manage_team").length == 0)
            return;

        var $avatar = $(".page_manage_team .avatars #avatar");
        var $flag = $(".page_manage_team .flags #flag");

        //events
        $('.page_manage_team .avatars #avatars-carousel').on('slid.bs.carousel', function ()
        {
            var value = $(".page_manage_team .avatars .item.active").attr("data-value");
            $avatar.val(value);
        });

        $(".page_manage_team .flags .dropdown-menu a").click(function (e)
        {
            e.preventDefault();

            var color = $(this).attr("data-value");

            //set value
            $flag.val(color);

            //selected image
            $(this).closest(".dropup").find(".dropdown-toggle").html($(this).html());

            //change avatars
            $(".page_manage_team .avatars .item img").each(function (index)
            {
                $(this).attr("src", "css/images/avatars/avatar" + (index + 1) + "_" + color + ".png");
            });
        });

        //init avatar
        $(".page_manage_team .avatars .item").removeClass("active");
        $(".page_manage_team .avatars .item[data-value='" + $avatar.val() + "']").addClass("active");

        //int flag
        $(".page_manage_team .flags .dropdown-menu a[data-value='" + $flag.val() + "']").click();
    }
};
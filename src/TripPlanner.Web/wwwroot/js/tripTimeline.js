// Vanilla JavaScript fullcalendar.io interop for the Blazor TripTimeline component.
// No jQuery dependency — uses the fullcalendar global bundle loaded in App.razor.
(function () {
    const tripTimeline = {
        create: function (element, startDate, endDate, events, dotNetRef) {
            if (!window.FullCalendar) {
                console.warn('FullCalendar global not loaded yet.');
                return null;
            }
            const calendar = new window.FullCalendar.Calendar(element, {
                initialView: 'timeGridWeek',
                initialDate: startDate,
                validRange: { start: startDate, end: addOneDay(endDate) },
                headerToolbar: { left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek,dayGridMonth' },
                height: 'auto',
                events: events,
                eventOrder: 'extendedProps.displayOrder,start',
                eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
                eventClick: function (info) {
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('SelectItemAsync', info.event.extendedProps.sourceType, info.event.id);
                    }
                }
            });
            calendar.render();
            const instance = {
                refresh: function (newStart, newEnd, newEvents) {
                    calendar.removeAllEvents();
                    calendar.setOption('validRange', { start: newStart, end: addOneDay(newEnd) });
                    calendar.gotoDate(newStart);
                    newEvents.forEach(function (e) { calendar.addEvent(e); });
                },
                dispose: function () { calendar.destroy(); }
            };
            return instance;
        }
    };

    function addOneDay(dateString) {
        const d = new Date(dateString + 'T00:00:00Z');
        d.setUTCDate(d.getUTCDate() + 1);
        return d.toISOString().substring(0, 10);
    }

    window.tripTimeline = tripTimeline;
})();

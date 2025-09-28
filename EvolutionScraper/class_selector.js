scrape = () => {

    const parseItalianDateTime = (dateString, timeString) => {
        // Italian day names mapping
        const italianDays = {
            'lunedì': 'Monday',
            'martedì': 'Tuesday',
            'mercoledì': 'Wednesday',
            'giovedì': 'Thursday',
            'venerdì': 'Friday',
            'sabato': 'Saturday',
            'domenica': 'Sunday'
        };

        // Italian month names mapping
        const italianMonths = {
            'gennaio': 0, 'febbraio': 1, 'marzo': 2, 'aprile': 3,
            'maggio': 4, 'giugno': 5, 'luglio': 6, 'agosto': 7,
            'settembre': 8, 'ottobre': 9, 'novembre': 10, 'dicembre': 11
        };

        // Parse the date string (format: "giorno DD mese YYYY")
        const dateParts = dateString.trim().split(' ');
        const day = parseInt(dateParts[1]);
        const monthName = dateParts[2].toLowerCase();
        const year = parseInt(dateParts[3]);
        const month = italianMonths[monthName];

        // Parse the time string (format: "HH:MM TIMEZONE")
        const timeParts = timeString.trim().split(/\s+/);
        const [hours, minutes] = timeParts[0].split(':').map(num => parseInt(num));
        const timezone = timeParts[1]; // CEST, CET, etc.

        // Create the date object
        // Note: CEST is UTC+2, CET is UTC+1
        const date = new Date(year, month, day, hours, minutes);

        // Adjust for timezone if needed (this creates a local time)
        // If you need UTC time, you might want to adjust based on timezone
        if (timezone === 'CEST') {
            // CEST is UTC+2 - if you want to convert to UTC, subtract 2 hours
            // date.setHours(date.getHours() - 2);
        } else if (timezone === 'CET') {
            // CET is UTC+1 - if you want to convert to UTC, subtract 1 hour  
            // date.setHours(date.getHours() - 1);
        }

        return date;
    }



    const findPreviousHeader = (row) => {
        let current = row.previousElementSibling;

        while (current) {
            if (current.classList.contains("header")) {
                return current;
            }
            current = current.previousElementSibling;
        }

        return null; // No header found
    }

    return Array
        .from(document.getElementsByClassName("modalClassDesc"))
        .map(x => {
            const parentRow = x?.parentElement?.parentElement?.parentElement;

            // Extract structured data from known positions
            const time = parentRow?.querySelector(".col-first")?.textContent.trim() || "";
            const className = parentRow?.querySelector(".modalClassDesc")?.textContent.trim() || "";
            const instructor = parentRow?.querySelector(".modalBio")?.textContent.trim() || "";
            const room = parentRow?.querySelector(".col-2 .col:nth-child(5)")?.textContent.trim() || "";
            const duration = parentRow?.querySelector(".col-2 .col:nth-child(6)")?.textContent.trim() || "";
            const availability = parentRow?.querySelector(".tablet-viewable")?.textContent.trim()
                || parentRow?.querySelector(".tablet-hidden")?.textContent.trim() || "";
            const button = parentRow?.querySelector(".SignupButton")?.getAttribute("name");
            const date = findPreviousHeader(parentRow)?.querySelector("b")?.textContent.trim() || "";

            return {
                className,
                instructor,
                room,
                duration,
                availability,
                button,
                date: parseItalianDateTime(date, time).toISOString()
            };

        });
}
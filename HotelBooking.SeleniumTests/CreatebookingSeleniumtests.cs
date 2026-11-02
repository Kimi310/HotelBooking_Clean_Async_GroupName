using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace HotelBooking.SeleniumTests
{
    /// <summary>
    /// UI tests for Views/Bookings/Create.cshtml of HotelBooking.Mvc.
    ///
    /// Assumes the MVC app runs in the Development environment, so the database is seeded with
    /// 2 rooms that are both booked from today+10 to today+20 (same data as the unit tests).
    /// Set HOTELBOOKING_MVC_URL to point at the running app.
    /// </summary>
    public class CreateBookingSeleniumTests : IDisposable
    {
        // ---- Locators taken from Create.cshtml / Delete.cshtml ----
        private const string CreatePath = "/Bookings/Create";
        private static readonly By StartDateInput = By.Id("StartDate");
        private static readonly By EndDateInput = By.Id("EndDate");
        private static readonly By CustomerSelect = By.Id("CustomerId");
        private static readonly By SubmitButton = By.CssSelector("form input[type='submit'][value='Create']");
        private static readonly By StatusMessage = By.CssSelector("h4.text-danger");   // @ViewBag.Status
        private static readonly By DeleteLinks = By.CssSelector("a[href*='/Bookings/Delete/']");
        private static readonly By ConfirmDeleteButton = By.CssSelector("form input[type='submit'][value='Delete']");
        // Text set by BookingsController in ViewBag.Status when no room is free
        private const string NoRoomMessage = "no available room";
        // -----------------------------------------------------------

        private const int OccupiedStartOffset = 10;
        private const int OccupiedEndOffset = 20;

        private readonly IWebDriver driver;
        private readonly string baseUrl =
            (Environment.GetEnvironmentVariable("HOTELBOOKING_MVC_URL") ?? "http://localhost:5002").TrimEnd('/');
        private readonly List<int> createdBookingIds = new List<int>();

        public CreateBookingSeleniumTests()
        {
            var options = new ChromeOptions();
            options.AddArguments("--headless=new", "--no-sandbox", "--disable-dev-shm-usage",
                                 "--ignore-certificate-errors", "--window-size=1280,900");
            driver = new ChromeDriver(options);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }

        public void Dispose()
        {
            try { CleanUpCreatedBookings(); }
            finally { driver.Quit(); }
        }

        #region Helpers

        private void OpenCreatePage() => driver.Navigate().GoToUrl(baseUrl + CreatePath);

        private void SetDate(By locator, DateTime? date)
        {
            // Setting the value through JS avoids locale-specific typing into <input type="date">
            var element = driver.FindElement(locator);
            var value = date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "";
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].value = arguments[1];", element, value);
        }

        private bool IsOnCreatePage() =>
            driver.Url.TrimEnd('/').EndsWith(CreatePath, StringComparison.OrdinalIgnoreCase);

        private IEnumerable<int> BookingIdsOnIndexPage()
        {
            // Index links look like /Bookings/Delete/12
            return driver.FindElements(DeleteLinks)
                .Select(a => Regex.Match(a.GetAttribute("href") ?? "", @"/Bookings/Delete/(\d+)"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();
        }

        /// <summary>
        /// Fills in the form, submits it and returns true if the app redirected away from Create
        /// (i.e. the booking was created). A newly created booking is remembered for clean-up.
        /// </summary>
        private bool SubmitBooking(DateTime? start, DateTime? end, int waitSeconds)
        {
            // Remember which bookings exist BEFORE we submit, so clean-up only ever deletes new ones
            driver.Navigate().GoToUrl(baseUrl + "/Bookings");
            var idsBefore = new HashSet<int>(BookingIdsOnIndexPage());

            OpenCreatePage();
            SetDate(StartDateInput, start);
            SetDate(EndDateInput, end);
            new SelectElement(driver.FindElement(CustomerSelect)).SelectByIndex(0);
            driver.FindElement(SubmitButton).Click();

            var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsOnCreatePage())
                {
                    RememberNewBookings(idsBefore);
                    return true;
                }
                Thread.Sleep(250);
            }
            return false;
        }

        private void RememberNewBookings(HashSet<int> idsBefore)
        {
            try
            {
                driver.Navigate().GoToUrl(baseUrl + "/Bookings");
                createdBookingIds.AddRange(BookingIdsOnIndexPage().Where(id => !idsBefore.Contains(id)));
            }
            catch (WebDriverException) { /* clean-up is best effort */ }
        }

        private void CleanUpCreatedBookings()
        {
            foreach (var id in createdBookingIds.Distinct())
            {
                try
                {
                    driver.Navigate().GoToUrl($"{baseUrl}/Bookings/Delete/{id}");
                    driver.FindElement(ConfirmDeleteButton).Click();
                }
                catch (WebDriverException) { /* best effort */ }
            }
        }

        private static DateTime Today(int offsetDays) => DateTime.Today.AddDays(offsetDays);

        #endregion

        #region Success

        [Fact]
        public void CreateBooking_ValidFutureDates_RedirectsToBookingsList()
        {
            var redirected = SubmitBooking(Today(1), Today(2), waitSeconds: 10);

            Assert.True(redirected, "Expected a redirect away from the Create page after a successful booking.");
            Assert.EndsWith("/Bookings", driver.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreateBooking_ValidFutureDates_ShowsNoErrorStatus()
        {
            SubmitBooking(Today(4), Today(5), waitSeconds: 10);
            OpenCreatePage();

            // A fresh Create page has an empty @ViewBag.Status heading
            Assert.True(string.IsNullOrWhiteSpace(driver.FindElement(StatusMessage).Text));
        }

        [Fact]
        public void CreateBooking_SingleDay_StartEqualsEnd_Succeeds()
        {
            var redirected = SubmitBooking(Today(3), Today(3), waitSeconds: 10);

            Assert.True(redirected);
        }

        [Fact]
        public void CreateBooking_FreePeriodAfterOccupiedDates_Succeeds()
        {
            var redirected = SubmitBooking(Today(OccupiedEndOffset + 10), Today(OccupiedEndOffset + 12), waitSeconds: 10);

            Assert.True(redirected);
        }

        #endregion

        #region No room available

        [Fact]
        public void CreateBooking_FullyOccupiedPeriod_StaysOnCreatePage()
        {
            var redirected = SubmitBooking(Today(OccupiedStartOffset), Today(OccupiedEndOffset), waitSeconds: 3);

            Assert.False(redirected, "Booking should be rejected when all rooms are occupied.");
            Assert.True(IsOnCreatePage());
        }

        [Fact]
        public void CreateBooking_FullyOccupiedPeriod_ShowsNoRoomMessage()
        {
            SubmitBooking(Today(OccupiedStartOffset), Today(OccupiedEndOffset), waitSeconds: 3);

            var status = driver.FindElement(StatusMessage).Text;
            Assert.Contains(NoRoomMessage, status, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(12, 15)]  // fully inside the occupied period
        [InlineData(5, 12)]   // starts before, ends inside
        [InlineData(18, 25)]  // starts inside, ends after
        [InlineData(5, 25)]   // surrounds the occupied period
        [InlineData(10, 10)]  // single day on the first occupied day
        [InlineData(20, 20)]  // single day on the last occupied day
        public void CreateBooking_OverlappingOccupiedPeriod_IsRejected(int startOffset, int endOffset)
        {
            var redirected = SubmitBooking(Today(startOffset), Today(endOffset), waitSeconds: 3);

            Assert.False(redirected);
            Assert.True(IsOnCreatePage());
        }

        #endregion

        #region Invalid input

        [Fact]
        public void CreateBooking_StartDateIsToday_IsRejected()
        {
            var redirected = SubmitBooking(Today(0), Today(2), waitSeconds: 3);

            Assert.False(redirected);
        }

        [Fact]
        public void CreateBooking_StartDateInThePast_IsRejected()
        {
            var redirected = SubmitBooking(Today(-3), Today(2), waitSeconds: 3);

            Assert.False(redirected);
        }

        [Fact]
        public void CreateBooking_StartDateAfterEndDate_IsRejected()
        {
            var redirected = SubmitBooking(Today(8), Today(5), waitSeconds: 3);

            Assert.False(redirected);
        }

        [Fact]
        public void CreateBooking_EmptyDates_IsRejected()
        {
            var redirected = SubmitBooking(null, null, waitSeconds: 3);

            Assert.False(redirected);
        }

        #endregion
    }
}

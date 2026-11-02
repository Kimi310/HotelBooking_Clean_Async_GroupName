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
    /// Knows how to use the Bookings pages (Index / Create / Delete) through a headless Chrome.
    /// Every booking created through <see cref="Submit"/> is remembered and deleted again on Dispose,
    /// so the tests leave the app as they found it.
    /// </summary>
    internal sealed class BookingsSite : IDisposable
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
        // -----------------------------------------------------------

        private readonly IWebDriver driver;
        private readonly string baseUrl =
            (Environment.GetEnvironmentVariable("HOTELBOOKING_MVC_URL") ?? "http://localhost:5002").TrimEnd('/');
        private readonly List<int> createdBookingIds = new List<int>();

        public BookingsSite()
        {
            var options = new ChromeOptions();
            options.AddArguments("--headless=new", "--no-sandbox", "--disable-dev-shm-usage",
                                 "--ignore-certificate-errors", "--window-size=1280,900");
            driver = new ChromeDriver(options);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }

        public void OpenCreatePage() => driver.Navigate().GoToUrl(baseUrl + CreatePath);

        public bool IsOnCreatePage() =>
            driver.Url.TrimEnd('/').EndsWith(CreatePath, StringComparison.OrdinalIgnoreCase);

        public string CurrentUrl => driver.Url;

        /// <summary>Text of the red heading that shows ViewBag.Status (empty when there is no message).</summary>
        public string StatusText() => driver.FindElement(StatusMessage).Text;

        /// <summary>
        /// Fills in the Create form and submits it.
        /// Returns true if the app redirected away from the Create page (= booking created).
        /// </summary>
        public bool Submit(DateTime? start, DateTime? end, int waitSeconds = 10)
        {
            // Remember which bookings exist BEFORE, so clean-up only ever deletes new ones
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

        private void SetDate(By locator, DateTime? date)
        {
            // Setting the value through JS avoids locale-specific typing into <input type="date">
            var element = driver.FindElement(locator);
            var value = date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "";
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].value = arguments[1];", element, value);
        }

        private List<int> BookingIdsOnIndexPage()
        {
            // Index links look like /Bookings/Delete/12
            return driver.FindElements(DeleteLinks)
                .Select(a => Regex.Match(a.GetAttribute("href") ?? "", @"/Bookings/Delete/(\d+)"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();
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

        public void Dispose()
        {
            try
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
            finally
            {
                driver.Quit();
            }
        }
    }

    /// <summary>
    /// Runs ONCE before the "no room" tests. It books the period today+40..today+50 again and again
    /// until the app refuses - that means every room (however many the seed data created) is full.
    /// This way the tests do not depend on the seed data's dates or on how many rooms exist.
    /// </summary>
    public sealed class OccupiedPeriodFixture : IDisposable
    {
        public const int StartOffset = 40;
        public const int EndOffset = 50;

        private readonly BookingsSite site = new BookingsSite();

        public OccupiedPeriodFixture()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var created = site.Submit(DateTime.Today.AddDays(StartOffset),
                                          DateTime.Today.AddDays(EndOffset), waitSeconds: 6);
                if (!created) return;   // refused => all rooms are now occupied
            }
            throw new InvalidOperationException("Could not fill all rooms: the app kept accepting bookings.");
        }

        public void Dispose() => site.Dispose();
    }

    /// <summary>
    /// UI tests for Views/Bookings/Create.cshtml of HotelBooking.Mvc.
    /// Run the MVC app in Development (it needs the seeded customers for the dropdown) and set
    /// HOTELBOOKING_MVC_URL if it is not on http://localhost:5002.
    /// </summary>
    public class CreateBookingSeleniumTests : IClassFixture<OccupiedPeriodFixture>, IDisposable
    {
        // Text set by BookingsController in ViewBag.Status when no room is free
        private const string NoRoomMessage = "no available room";

        private const int OccStart = OccupiedPeriodFixture.StartOffset;   // 40
        private const int OccEnd = OccupiedPeriodFixture.EndOffset;       // 50
        private const int FreeFrom = 70;   // far away from the occupied period

        private readonly BookingsSite site = new BookingsSite();

        public CreateBookingSeleniumTests(OccupiedPeriodFixture occupiedPeriod)
        {
            // The fixture has already filled every room for OccStart..OccEnd
        }

        public void Dispose() => site.Dispose();

        private static DateTime Today(int offsetDays) => DateTime.Today.AddDays(offsetDays);

        #region Success

        [Fact]
        public void CreateBooking_ValidFutureDates_RedirectsToBookingsList()
        {
            var created = site.Submit(Today(FreeFrom), Today(FreeFrom + 1));

            Assert.True(created, "Expected a redirect away from the Create page after a successful booking.");
            Assert.EndsWith("/Bookings", site.CurrentUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreateBooking_ValidFutureDates_ShowsNoErrorStatus()
        {
            site.Submit(Today(FreeFrom + 3), Today(FreeFrom + 4));
            site.OpenCreatePage();

            // A fresh Create page has an empty @ViewBag.Status heading
            Assert.True(string.IsNullOrWhiteSpace(site.StatusText()));
        }

        [Fact]
        public void CreateBooking_SingleDay_StartEqualsEnd_Succeeds()
        {
            var created = site.Submit(Today(FreeFrom + 6), Today(FreeFrom + 6));

            Assert.True(created);
        }

        [Fact]
        public void CreateBooking_DayRightAfterOccupiedPeriod_Succeeds()
        {
            // Last occupied day is OccEnd, so OccEnd + 1 must be free
            var created = site.Submit(Today(OccEnd + 1), Today(OccEnd + 2));

            Assert.True(created);
        }

        [Fact]
        public void CreateBooking_DayRightBeforeOccupiedPeriod_Succeeds()
        {
            // First occupied day is OccStart, so OccStart - 1 must be free
            var created = site.Submit(Today(OccStart - 2), Today(OccStart - 1));

            Assert.True(created);
        }

        #endregion

        #region No room available

        [Fact]
        public void CreateBooking_FullyOccupiedPeriod_StaysOnCreatePage()
        {
            var created = site.Submit(Today(OccStart), Today(OccEnd), waitSeconds: 3);

            Assert.False(created, "Booking should be rejected when all rooms are occupied.");
            Assert.True(site.IsOnCreatePage());
        }

        [Fact]
        public void CreateBooking_FullyOccupiedPeriod_ShowsNoRoomMessage()
        {
            site.Submit(Today(OccStart), Today(OccEnd), waitSeconds: 3);

            Assert.Contains(NoRoomMessage, site.StatusText(), StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(2, 5)]    // fully inside the occupied period (OccStart + 2 .. OccStart + 5)
        [InlineData(-5, 2)]   // starts before, ends inside
        [InlineData(8, 15)]   // starts inside, ends after
        [InlineData(-5, 15)]  // surrounds the occupied period
        [InlineData(0, 0)]    // single day on the first occupied day
        [InlineData(10, 10)]  // single day on the last occupied day
        public void CreateBooking_OverlappingOccupiedPeriod_IsRejected(int startRelative, int endRelative)
        {
            // Offsets are relative to OccStart; the occupied period is OccStart .. OccStart + 10
            var created = site.Submit(Today(OccStart + startRelative), Today(OccStart + endRelative), waitSeconds: 3);

            Assert.False(created);
            Assert.True(site.IsOnCreatePage());
        }

        #endregion

        #region Invalid input

        [Fact]
        public void CreateBooking_StartDateIsToday_IsRejected()
        {
            var created = site.Submit(Today(0), Today(2), waitSeconds: 3);

            Assert.False(created);
        }

        [Fact]
        public void CreateBooking_StartDateInThePast_IsRejected()
        {
            var created = site.Submit(Today(-3), Today(2), waitSeconds: 3);

            Assert.False(created);
        }

        [Fact]
        public void CreateBooking_StartDateAfterEndDate_IsRejected()
        {
            var created = site.Submit(Today(8), Today(5), waitSeconds: 3);

            Assert.False(created);
        }

        [Fact]
        public void CreateBooking_EmptyDates_IsRejected()
        {
            var created = site.Submit(null, null, waitSeconds: 3);

            Assert.False(created);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelBooking.Core;
using Moq;
using Xunit;

namespace HotelBooking.UnitTests
{
    public class BookingManagerMoqTests
    {
        private readonly Mock<IRepository<Booking>> bookingRepositoryMock;
        private readonly Mock<IRepository<Room>> roomRepositoryMock;
        private readonly BookingManager bookingManager;

        private readonly DateTime occupiedStart = DateTime.Today.AddDays(10);
        private readonly DateTime occupiedEnd = DateTime.Today.AddDays(20);

        public BookingManagerMoqTests()
        {
            var rooms = new List<Room>
            {
                new Room { Id = 1, Description = "A" },
                new Room { Id = 2, Description = "B" }
            };

            var bookings = new List<Booking>
            {
                new Booking { Id = 1, StartDate = occupiedStart, EndDate = occupiedEnd, IsActive = true, RoomId = 1 },
                new Booking { Id = 2, StartDate = occupiedStart, EndDate = occupiedEnd, IsActive = true, RoomId = 2 }
            };

            bookingRepositoryMock = new Mock<IRepository<Booking>>();
            bookingRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(bookings);

            roomRepositoryMock = new Mock<IRepository<Room>>();
            roomRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rooms);

            bookingManager = new BookingManager(bookingRepositoryMock.Object, roomRepositoryMock.Object);
        }

        #region FindAvailableRoom
        public static IEnumerable<object[]> InvalidDateRanges()
        {
            yield return new object[] { DateTime.Today, DateTime.Today.AddDays(5) };
            yield return new object[] { DateTime.Today.AddDays(-3), DateTime.Today.AddDays(5) };
            yield return new object[] { DateTime.Today.AddDays(10), DateTime.Today.AddDays(5) };
        }

        [Theory]
        [MemberData(nameof(InvalidDateRanges))]
        public async Task FindAvailableRoom_InvalidDates_ThrowsArgumentException(DateTime start, DateTime end)
        {
            // Act
            Task Act() => bookingManager.FindAvailableRoom(start, end);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(Act);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_ReturnsRoomId()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);

            // Act
            var roomId = await bookingManager.FindAvailableRoom(date, date);

            // Assert
            Assert.NotEqual(-1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_ReturnsRoomWithoutOverlappingBooking()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);

            // Act
            var roomId = await bookingManager.FindAvailableRoom(date, date);

            var bookings = await bookingRepositoryMock.Object.GetAllAsync();
            var overlapping = bookings.Where(b => b.RoomId == roomId
                                                  && b.IsActive
                                                  && b.StartDate <= date
                                                  && b.EndDate >= date);

            // Assert
            Assert.Empty(overlapping);
        }

        [Fact]
        public async Task FindAvailableRoom_AllRoomsOccupied_ReturnsMinusOne()
        {
            // Act 
            var roomId = await bookingManager.FindAvailableRoom(occupiedStart, occupiedEnd);

            // Assert
            Assert.Equal(-1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_QueriesBothRepositories()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);

            // Act
            await bookingManager.FindAvailableRoom(date, date);

            // Assert
            bookingRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
            roomRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
        }

        #endregion

        #region CreateBooking

        [Fact]
        public async Task CreateBooking_RoomAvailable_AddsBookingAndReturnsTrue()
        {
            // Arrange
            var booking = new Booking
            {
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(2)
            };

            // Act
            var created = await bookingManager.CreateBooking(booking);

            // Assert
            Assert.True(created);
            Assert.True(booking.IsActive);
            Assert.True(booking.RoomId > 0);
            bookingRepositoryMock.Verify(r => r.AddAsync(booking), Times.Once);
        }

        [Fact]
        public async Task CreateBooking_NoRoomAvailable_DoesNotAddAndReturnsFalse()
        {
            // Arrange
            var booking = new Booking
            {
                StartDate = occupiedStart,
                EndDate = occupiedEnd
            };

            // Act
            var created = await bookingManager.CreateBooking(booking);

            // Assert
            Assert.False(created);
            Assert.False(booking.IsActive);
            bookingRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Booking>()), Times.Never);
        }

        #endregion

        #region GetFullyOccupiedDates

        [Fact]
        public async Task GetFullyOccupiedDates_StartAfterEnd_ThrowsArgumentException()
        {
            // Arrange
            var start = DateTime.Today.AddDays(20);
            var end = DateTime.Today.AddDays(10);

            // Act
            Task Act() => bookingManager.GetFullyOccupiedDates(start, end);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(Act);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_PeriodFullyBooked_ReturnsAllDatesInPeriod()
        {
            // Act 
            var result = await bookingManager.GetFullyOccupiedDates(occupiedStart, occupiedEnd);

            // Assert
            var expectedCount = (occupiedEnd - occupiedStart).Days + 1;
            Assert.Equal(expectedCount, result.Count);
            Assert.All(result, d => Assert.InRange(d, occupiedStart, occupiedEnd));
        }

        [Fact]
        public async Task GetFullyOccupiedDates_PeriodBeforeBookings_ReturnsEmptyList()
        {
            // Arrange 
            var start = DateTime.Today.AddDays(1);
            var end = DateTime.Today.AddDays(2);

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(start, end);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_NoBookingsAtAll_ReturnsEmptyList()
        {
            // Arrange
            bookingRepositoryMock.Setup(r => r.GetAllAsync())
                                 .ReturnsAsync(new List<Booking>());

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(occupiedStart, occupiedEnd);

            // Assert
            Assert.Empty(result);
        }
        
        [Theory]
        [InlineData(10, 12)]
        [InlineData(15, 20)]
        [InlineData(10, 25)]
        public async Task GetFullyOccupiedDates_OnlyOneRoomBooked_ReturnsEmptyList(int startOffset, int endOffset)
        {
            // Arrange
            bookingRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Booking>
            {
                new Booking { Id = 1, StartDate = occupiedStart, EndDate = occupiedEnd, IsActive = true, RoomId = 1 }
            });

            var start = DateTime.Today.AddDays(startOffset);
            var end = DateTime.Today.AddDays(endOffset);

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(start, end);

            // Assert
            Assert.Empty(result);
        }

        #endregion
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelBooking.Core;
using Reqnroll;
using Xunit;

namespace HotelBooking.CucumberTests.StepDefinitions
{
    [Binding]
    public class CreateBookingSteps
    {
        private InMemoryRepository<Booking> bookings;
        private InMemoryRepository<Room> rooms;
        private BookingManager bookingManager;
        private bool? created;
        private Exception thrown;

        [Given(@"all rooms are fully occupied from day (-?\d+) to day (-?\d+)")]
        public void GivenAllRoomsAreFullyOccupied(int startDay, int endDay)
        {
            rooms = new InMemoryRepository<Room>();
            bookings = new InMemoryRepository<Booking>();
            rooms.Items.Add(new Room { Id = 1, Description = "A" });
            rooms.Items.Add(new Room { Id = 2, Description = "B" });

            foreach (var room in rooms.Items)
            {
                bookings.Items.Add(new Booking
                {
                    Id = room.Id,
                    StartDate = DateTime.Today.AddDays(startDay),
                    EndDate = DateTime.Today.AddDays(endDay),
                    IsActive = true,
                    CustomerId = 1,
                    RoomId = room.Id
                });
            }

            bookingManager = new BookingManager(bookings, rooms);
        }

        [When(@"I create a booking from day (-?\d+) to day (-?\d+)")]
        public async Task WhenICreateABooking(int startDay, int endDay)
        {
            var booking = new Booking
            {
                StartDate = DateTime.Today.AddDays(startDay),
                EndDate = DateTime.Today.AddDays(endDay),
                CustomerId = 1
            };

            try
            {
                created = await bookingManager.CreateBooking(booking);
            }
            catch (ArgumentException ex)
            {
                thrown = ex;
            }
        }

        [Then(@"the booking is created")]
        public void ThenTheBookingIsCreated()
        {
            Assert.Null(thrown);
            Assert.True(created);
            Assert.Equal(3, bookings.Items.Count);
        }

        [Then(@"the booking is rejected")]
        public void ThenTheBookingIsRejected()
        {
            Assert.Null(thrown);
            Assert.False(created);
            Assert.Equal(2, bookings.Items.Count);
        }

        [Then(@"the booking is rejected with an invalid dates error")]
        public void ThenTheBookingIsRejectedWithAnInvalidDatesError()
        {
            Assert.IsType<ArgumentException>(thrown);
            Assert.Equal(2, bookings.Items.Count);
        }
    }

    public class InMemoryRepository<T> : IRepository<T>
    {
        public List<T> Items { get; } = new List<T>();

        public Task<IEnumerable<T>> GetAllAsync() => Task.FromResult<IEnumerable<T>>(Items.ToList());
        public Task<T> GetAsync(int id) => Task.FromResult(Items.FirstOrDefault());
        public Task AddAsync(T entity) { Items.Add(entity); return Task.CompletedTask; }
        public Task EditAsync(T entity) => Task.CompletedTask;
        public Task RemoveAsync(int id) => Task.CompletedTask;
    }
}

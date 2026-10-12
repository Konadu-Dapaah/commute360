books = {
    1: "Twisted Games",
    2: "Twisted Lies",
    3: "Twisted Hate",
    4: "Twisted Love",
    5: "Twisted Fate"
}
cart = {
    1: 0,
    2: 0,
    3: 0,
    4: 0,
    5: 0
}

def menu():
    print("Welcome to KONADU's BOOKSHOP ><")
    print("Twisted Series by Ana Huang")
    print("1.Twisted Games")
    print("2.Twisted Lies")
    print("3.Twisted Hate")
    print("4.Twisted Love")
    print("5.Twisted Fate")

    book = int(input("Enter book to purchase: "))
    copies = int(input("Enter number of copies to purchase: "))

    cart[book] = cart[book] + copies

    user_input()


def user_input():
    choice = input("Do you want to purchase another book ('yes'/'no'): ")
    if choice == "yes":
        menu()
    else:
        show_receipt()

def count(c1,c2,c3,c4,c5):
    count =0
    if c1 > 0:
        count += 1
    if c2 > 0:
        count += 1
    if c3 > 0:
        count += 1
    if c4 > 0:
        count += 1
    if c5 > 0:
        count += 1
    return count

def show_receipt():
    print("Your Receipt")
    total_cost = 0.0
    total_books = 0

    for book_id, copies in cart.items():
        if copies > 0:
            print(f"{books[book_id]}: {copies} copies")
            total_books += copies

    c1 = cart[1]
    c2 = cart[2]
    c3 = cart[3]
    c4 = cart[4]
    c5 = cart[5]

    while c1 > 0 or c2 > 0 or c3 > 0 or c4 > 0 or c5 > 0:

        different_books = count(c1,c2,c3,c4,c5)
        if different_books == 5:
            total_cost = total_cost + (5 * 8 * 0.75)
            c1 = c1 - 1
            c2 = c2 - 1
            c3 = c3 - 1
            c4 = c4 - 1
            c5 = c5 - 1
        elif different_books == 4:
            total_cost = total_cost + (4 * 8 * 0.80)
            remaining_books = 0
            if c1 > 0 and remaining_books < 4:
                c1 = c1 - 1
                remaining_books = remaining_books + 1
            if c2 > 0 and remaining_books < 4:
                c2 = c2 - 1
                remaining_books = remaining_books + 1
            if c3 > 0 and remaining_books < 4:
                c3 = c3 - 1
                remaining_books = remaining_books + 1
            if c4 > 0 and remaining_books < 4:
                c4 = c4 - 1
                remaining_books = remaining_books + 1
            if c5 > 0 and remaining_books < 4:
                c5 = c5 - 1
                remaining_books = remaining_books + 1
        elif different_books == 3:
            total_cost = total_cost + (3 * 8 * 0.90)
            remaining_books = 0
            if c1 > 0 and remaining_books < 3:
                c1 = c1 - 1
                remaining_books = remaining_books + 1
            if c2 > 0 and remaining_books < 3:
                c2 = c2 - 1
                remaining_books = remaining_books + 1
            if c3 > 0 and remaining_books < 3:
                c3 = c3 - 1
                remaining_books = remaining_books + 1
            if c4 > 0 and remaining_books < 3:
                c4 = c4 - 1
                remaining_books = remaining_books + 1
            if c5 > 0 and remaining_books < 3:
                c5 = c5 - 1
                remaining_books = remaining_books + 1
        elif different_books == 2:
            total_cost = total_cost + (2 * 8 * 0.95)
            remaining_books = 0
            if c1 > 0 and remaining_books < 2:
                c1 = c1 - 1
                remaining_books = remaining_books + 1
            if c2 > 0 and remaining_books < 2:
                c2 = c2 - 1
                remaining_books = remaining_books + 1
            if c3 > 0 and remaining_books < 2:
                c3 = c3 - 1
                remaining_books = remaining_books + 1
            if c4 > 0 and remaining_books< 2:
                c4 = c4 - 1
                remaining_books = remaining_books + 1
            if c5 > 0 and remaining_books < 2:
                c5 = c5 - 1
                remaining_books = remaining_books + 1
        elif different_books == 1:
            total_cost = total_cost + 8
            if c1 > 0:
                c1 = c1 - 1
            elif c2 > 0:
                c2 = c2 - 1
            elif c3 > 0:
                c3 = c3 - 1
            elif c4 > 0:
                c4 = c4 - 1
            elif c5 > 0:
                c5 = c5 - 1

    print(f"Total number of books bought: {total_books}")
    print(f"Your total cost is: ${total_cost}")


menu()
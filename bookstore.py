books = {
    1: "Twisted Games",
    2: "Twisted Lies",
    3: "Twisted Hate",
    4: "Twisted Love",
    5: "Twisted Fate"
}

original_price = 8

cart = {}

discount_table = {
    1: 0.00,
    2: 0.05,
    3: 0.10,
    4: 0.20,
    5: 0.25
}

memo = {}


def menu():
    print("Welcome to KONADU's BOOKSHOP ><")
    print("Twisted Series by Ana Huang")
    print("1.Twisted Games ")
    print("2.Twisted Lies ")
    print("3.Twisted Hate ")
    print("4.Twisted Love ")
    print("5.Twisted Fate ")

    book_selected = int(input("Enter book to purchase: "))
    copies_bought = int(input("Enter number of copies to purchase: "))

    if book_selected in cart:
        cart[book_selected] = cart[book_selected] + copies_bought
    else:
        cart[book_selected] = copies_bought

    user_input()


def user_input():
    while True:
        choice = input("Do you want to purchase another book ('yes'/'no'): ")
        if choice == "yes":
            menu()
            break
        elif choice == "no":
            break

    if choice == "no":
        show_receipt()


def get_best_price(counts):
    new_list = []
    for c in counts:
        if c > 0:
            new_list.append(c)
    counts = new_list

    if len(counts) == 0:
        return 0.0

    counts.sort(reverse=True)
    key = tuple(counts)

    if key in memo:
        return memo[key]

    number_of_titles = len(counts)
    best_cost_so_far = None

    for group_size in range(1, number_of_titles + 1):
        new_counts = counts.copy()

        for i in range(group_size):
            new_counts[i] = new_counts[i] - 1

        discount = discount_table[group_size]
        price_of_this_group = group_size * original_price * (1 - discount)

        cost_of_rest = get_best_price(new_counts)
        total_this_way = price_of_this_group + cost_of_rest

        if best_cost_so_far == None:
            best_cost_so_far = total_this_way
        elif total_this_way < best_cost_so_far:
            best_cost_so_far = total_this_way

    memo[key] = best_cost_so_far
    return best_cost_so_far


def show_receipt():
    print("")
    print("--- YOUR RECEIPT ---")
    total_books_bought = 0

    for book_number in cart:
        quantity = cart[book_number]
        total_books_bought = total_books_bought + quantity
        print(books[book_number] + " x " + str(quantity))

    counts_list = []
    for book_number in cart:
        counts_list.append(cart[book_number])

    total_cost = get_best_price(counts_list)

    print("")
    print("Total number of books bought: " + str(total_books_bought))
    print("Your total cost is: $" + str(round(total_cost, 2)))


menu()